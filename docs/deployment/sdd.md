# 部署与本地环境（Deployment）设计规格（SDD）

> 关联：[SDD](./sdd.md) ｜ [BDD](./bdd.md) ｜ [TDD](./tdd.md) ｜ [SOP](./sop.md) ｜ 上游：[../infra/sdd.md](../infra/sdd.md)（SystemOptions/配置加载链） ｜ 证据：docker compose 命令（见 [TDD](./tdd.md)）
> 规范：[../DOC-STANDARD.md](../DOC-STANDARD.md)。行为场景见 BDD（@DEP-Sxx），本文不重复。

## 目标与形态

| 形态 | 组成 | 状态 |
|---|---|---|
| 一体镜像（前后端同源） | 前端编译进 `wwwroot`，由 .NET 托管，无前后端分离 | ✅ 构建成功（2026-09-03） |
| A. Docker 单容器 | 仅 `moai` 镜像；外部提供 PG/Redis/MQ/OSS/沙箱 | 待执行（[@DEP-S17](./bdd.md#dep-s17)） |
| B. Docker Compose 一体 | pgvector(pg16) + redis7 + rabbitmq3 + **rustfs**(替代 MinIO) + **opensandbox-server** + moai | 待执行（[@DEP-S13](./bdd.md#dep-s13)） |
| 本地开发 | 后端 `dotnet run`（MAI_FILE 注入）+ 前端 `npm run dev` + 基础设施容器 | ✅ 当前实际可用形态（[@DEP-S12](./bdd.md#dep-s12)） |

覆盖对象：`Dockerfile`、`docker-entrypoint.sh`、`docker-compose.yml`、`.env.example`、`init-pgvector.sql`、`configs/system.json`、`deploy/{publish.sh,deploy-compose.sh,opensandbox/*}`。操作细则见 [docker.md](./docker.md)。

## 配置注入链（核心设计）

- **统一机制：`configs/system.json` 穿透映射**（[docker.md 第 3 节](./docker.md)）。后端加载器读 `MAI_FILE`（默认 `/app/configs/system.json`）；compose/`docker run` 把宿主机文件 bind-mount 进容器（[@DEP-S15](./bdd.md#dep-s15)）。模板按最新 `appsettings.Development.json` 结构生成，覆盖 `OpenSandBox`/`Storage(S3)`/`OTLP` 等字段。
- **对外地址占位符**：`Server`/`WebUI`/`Storage.Endpoint` 用 `__MOAI_HOST__`/`__MOAI_PORT__`/`__S3_PORT__`，entrypoint 按 `MOAI_HOST`/`MOAI_PORT`/`S3_PORT` 替换为运行时配置（`deploy-compose.sh` 自动探测本机 IP 写入 `.env`）。因预签名 URL 与前端 `/static` 前缀都用该 host，必须是「容器与浏览器都可达」的地址（[docker.md 3.1](./docker.md)）。
- 未挂载时：镜像内置 `/app/configs/system.json.template`，entrypoint 复制为默认配置；宿主机缺失文件导致 docker 建目录时 entrypoint 明确报错退出。
- **本地开发形态**：`MAI_FILE=<绝对路径>/system.local.json` 显式注入，绕过仓库内 `configs/system.json`（[@DEP-S11](./bdd.md#dep-s11)）。
- as-built 记录：旧 entrypoint 用 heredoc 从 `.env` 生成配置（且 `Storage.LocalPath` 字段已不存在），**2026-09-23 改为 system.json 挂载 + 内置模板**，`MAI_FILE` 显式导出。

## Dockerfile（三阶段）

1. `frontend-builder`（node:22-slim）：`COPY ui/package*.json` → `npm ci` → 复制源码 → 删 lock 重装（绕 Rollup 可选依赖问题）→ `npm run build`（不注入 `VITE_ServerUrl`，生产同源）。（原 `COPY ui/moai/...` 路径缺陷 D1，已修复）**无 apt 工具链步骤**。
2. `backend-builder`（**sdk:10.0.203**）：复制 `Directory.Packages.props` + `Directory.Build.props` + `src/` → restore/build/publish `src/MoAI/MoAI.csproj`。
3. `final`（**aspnet:10.0**）：publish 产物（含 `wwwroot/embed`）+ 前端 dist → `/app/wwwroot`；`configs/system.json` → `/app/configs/system.json.template`；`docker-entrypoint.sh`；`ENV MAI_FILE`；`EXPOSE 8080`。

## docker-compose.yml

- `postgres`：`pgvector/pgvector:pg16`，挂载 `init-pgvector.sql`（[@DEP-S8](./bdd.md#dep-s8)），healthcheck `pg_isready`。
- `redis`：`redis:7-alpine`（AOF）；`rabbitmq`：`3-management-alpine`（5672 + 15672）。
- `rustfs`：`rustfs/rustfs:latest`（S3 兼容，替代 MinIO；`RUSTFS_ACCESS_KEY/SECRET`），`rustfs-init` 幂等建桶（[@DEP-S14](./bdd.md#dep-s14)）。
- `opensandbox-server`：官方 `opensandbox/server:latest`，挂载 `docker.sock` + `deploy/opensandbox/sandbox.toml`（`resolve_internal=false`、`host_ip=host.docker.internal`），独立容器避免 .NET 侧持有 Docker 能力（[@DEP-S16](./bdd.md#dep-s16)）。
- `sandbox-image`：`profiles: ["sandbox-images"]`，仅供 `pull`（沙箱运行时镜像只拉取不部署）。
- `moai`：默认镜像 `whuanle/moai:latest`（本地可 build），`depends_on` 三者 `service_healthy` + `rustfs-init` 完成 + `opensandbox-server` 启动（[@DEP-S7](./bdd.md#dep-s7)）；bind-mount `configs/system.json`；volume `moai_files:/app/files`。
- 图数据库（Memgraph）**不内置**，按 [docker.md 第 7 节](./docker.md) 单独部署接入。
- 网络 `moai-network`（bridge）；named volume：postgres_data/redis_data/rabbitmq_data/rustfs_data/rustfs_logs/opensandbox_data/moai_files。

## 已知缺陷（D1/D2 已修复，D3–D5 记录中）

| # | 问题 | 影响 | 状态/规避 |
|---|---|---|---|
| D1 | Dockerfile 前端阶段曾 `COPY ui/moai/...`，仓库实际目录是 `ui/` | 曾导致**镜像构建直接失败**（[@DEP-S1](./bdd.md#dep-s1)） | ✅ **已修复（2026-09-02）**：两处改 `ui/`，镜像同步升 net10，`MAI_CONFIG` 改 `MAI_FILE` |
| D2 | `ConfigureOpenTelemetryModule` 曾对 `OTLP.Trace/Metrics` **无条件 `new Uri(...)`**，而 entrypoint/compose 对 OTLP 默认留空 | 曾导致**默认 `docker-compose up` 后端启动即抛异常**（[@DEP-S4](./bdd.md#dep-s4)） | ✅ **已修复（2026-09-02）**：新增 `ParseOtlpEndpoint`，空值/非法地址跳过导出，OTLP 变为可选项 |
| D3 | ~~entrypoint 只生成 `Storage.LocalPath`~~（该字段在 `SystemOptionStorage` 中**不存在**，S3 五字段全空 → S3 客户端无 ServiceURL，业务接口 500） | 曾导致容器形态上传类接口必 500（[@DEP-S3](./bdd.md#dep-s3)） | ✅ **已修复**：2026-09-03 由 entrypoint 生成 S3 段；**2026-09-23 改为 `configs/system.json` 模板直接携带 `Storage` 五项**（挂载注入）。**部署注意**：`Storage.Endpoint` 单端点设计——预签名 URL 的 host 即应用所配端点，必须**同时**对应用与上传客户端（浏览器）可达，见 [docker.md 3.1](./docker.md) |
| D7 | 沙箱需 Docker + Python，若与 .NET 同容器会扩大逃逸面 | 安全风险 | ✅ **已规避（2026-09-23）**：`opensandbox-server` 拆为独立容器（官方镜像），.NET 仅 HTTP 调用（[@DEP-S16](./bdd.md#dep-s16)） |
| D8 | 图数据库社区版默认纯内存，容器重启丢图 | 知识图谱数据丢失 | 不内置；接入时用 `--storage-snapshot-interval-sec` + `--storage-snapshot-on-exit=true`（[docker.md 第 7 节](./docker.md)） |
| D4 | `.env.example` 的 OTLP 示例指 `127.0.0.1:4012`，容器内 `127.0.0.1` 是容器自身 | 照抄示例则 OTLP 上报失败（[@DEP-S6](./bdd.md#dep-s6)） | 写 collector 的容器网络名或宿主机地址 |
| D5 | README 引用的 `moai_docs/` 目录已不存在于仓库 | README 图片裂图 | 与部署无关，仅记录 |
| D6 | aspnet:10.0 运行时无 `libgssapi_krb5.so.2`，启动时探测告警（Cannot load library） | **实测不影响功能**（容器内 e2e 41/41 全过，Negotiate 探测为非致命）；如需消除在 final 阶段加装 `libgssapi-krb5-2` | 记录，暂不处理（构建 VM 内 apt 不可用，装库需离线 .deb 方案） |

## Apple Silicon 构建注意事项（2026-09-03 实测）

- `mcr.microsoft.com/dotnet/{sdk,aspnet}:10.0` **均有 linux/arm64**，Apple Silicon 上直接原生构建（默认平台）即可，实测通过。
- **不要**用 `--platform linux/amd64` + QEMU 仿真构建：dotnet restore 会随机 `SIGSEGV`（qemu signal 11）或 `MSB4184 GetTargetFrameworkVersion` 异常——两者均为仿真伪故障，原生 amd64 CI 不受影响（Dockerfile 注释有记）。
- Docker VM 内 `deb.debian.org`（HTTP:80）曾出现 DNS 瞬断/502（代理拦截），apt 步骤已移除（D1 修复说明）；npm/nuget 走 HTTPS 正常。

## 本地开发环境（当前实际形态）

- 基础设施容器（自定义端口）：`moai-postgres` 5432、`moai-redis` 55379、`moai-rabbitmq` 55672（管理台 15673）、`moai-minio` 9000/9001（桶 `moai`，公共读）。
- 后端：`src/MoAI` 下 `MAI_FILE=<path>/system.local.json ASPNETCORE_ENVIRONMENT=Development dotnet run`，监听 5210/5211（Kestrel 双端口：`MoAI:Port` 与 Port+1）。
- 前端：`ui/` 下 `npm run dev`（4000），`VITE_ServerUrl=http://127.0.0.1:5210`。
- 种子账号 admin/abcd123456（root，id=1）；pgvector 扩展由首次建库 SQL 或应用 EnsureCreated 双保险保证（见 [../database-scaffold/sdd.md](../database-scaffold/sdd.md)）。
