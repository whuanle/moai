# 部署与本地环境（Deployment）设计规格（SDD）

> 关联：[SDD](./sdd.md) ｜ [BDD](./bdd.md) ｜ [TDD](./tdd.md) ｜ [SOP](./sop.md) ｜ 上游：[../infra/sdd.md](../infra/sdd.md)（SystemOptions/配置加载链） ｜ 证据：docker compose 命令（见 [TDD](./tdd.md)）
> 规范：[../DOC-STANDARD.md](../DOC-STANDARD.md)。行为场景见 BDD（@DEP-Sxx），本文不重复。

## 目标与形态

| 形态 | 组成 | 状态 |
|---|---|---|
| Docker Compose 一键部署 | pgvector(pg16) + redis7 + rabbitmq3 + moai 后端镜像（含前端静态资源） | ✅ **容器实测通过（2026-09-03）**：镜像构建成功 + 起容器 serverinfo 200 + e2e 41/41（UM 34 + 存储 7，[@DEP-S2](./bdd.md#dep-s2)） |
| 本地开发 | 后端 `dotnet run`（MAI_FILE 注入）+ 前端 `npm run dev` + 四个基础设施容器 | ✅ 当前实际可用形态（[@DEP-S12](./bdd.md#dep-s12)） |

覆盖对象：`Dockerfile`、`docker-entrypoint.sh`、`docker-compose.yml`、`.env.example`、`init-pgvector.sql`、`configs/system.json` 回退链。

## 配置注入链（核心设计）

- **容器形态**：`.env` → compose 环境变量 → `docker-entrypoint.sh` 用 heredoc 生成 `/app/configs/system.json` → 后端启动。工作目录 `/app`，`MAI_FILE` 未设置时加载器回退到 `configs/system.json`（相对 ContentRoot），容器内无需 MAI_FILE（[@DEP-S10](./bdd.md#dep-s10)）。
- **本地开发形态**：`MAI_FILE=<绝对路径>/system.local.json` 显式注入，绕过仓库内过时的 `configs/system.json`（Port=5000 且为 Windows 路径）（[@DEP-S11](./bdd.md#dep-s11)）。
- as-built 记录：Dockerfile 曾用无效变量 `ENV MAI_CONFIG=...`（加载器识别的是 `MAI_FILE`），**2026-09-02 已修正为 `ENV MAI_FILE=/app/configs/system.json`**，现与回退链双保险。

## Dockerfile（三阶段）

1. `frontend-builder`（node:22-slim）：`COPY ui/package*.json` → `npm ci` → 复制源码 → 删 lock 重装（绕 Rollup 可选依赖问题）→ `npm run build`。（原 `COPY ui/moai/...` 路径缺陷 D1，已修复）**无 apt 工具链步骤**：依赖均为纯 JS/预编译二进制，无需 node-gyp（曾因 Docker VM 内 deb.debian.org DNS/502 反复失败，2026-09-03 移除后构建通过）。
2. `backend-builder`（**sdk:10.0.203**，钉版本保证可复现）：复制 `Directory.Packages.props` + `Directory.Build.props` + `src/` → restore/build/publish `src/MoAI/MoAI.csproj`。
3. `final`（**aspnet:10.0**）：publish 产物 + 前端 dist → `/app/wwwroot`（后端静态托管 + SPA 回退）+ `docker-entrypoint.sh`。

## docker-compose.yml

- `postgres`：`pgvector/pgvector:pg16`，挂载 `init-pgvector.sql` 到 `/docker-entrypoint-initdb.d/`（仅首次建卷执行，[@DEP-S8](./bdd.md#dep-s8)），healthcheck `pg_isready`。
- `redis`：`redis:7-alpine`，AOF 开启，healthcheck `redis-cli ping`；`rabbitmq`：`3-management-alpine`（5672 + 管理 UI 15672）。
- `moai`：默认镜像 `registry.cn-hangzhou.aliyuncs.com/whuanle/moai:latest`（本地可 build），`depends_on` 三者 `service_healthy`（[@DEP-S7](./bdd.md#dep-s7)）；volume `moai_files:/app/files`。
- 网络 `moai-network`（bridge）；四个 named volume（postgres_data/redis_data/rabbitmq_data/moai_files）。

## 已知缺陷（D1/D2 已修复，D3–D5 记录中）

| # | 问题 | 影响 | 状态/规避 |
|---|---|---|---|
| D1 | Dockerfile 前端阶段曾 `COPY ui/moai/...`，仓库实际目录是 `ui/` | 曾导致**镜像构建直接失败**（[@DEP-S1](./bdd.md#dep-s1)） | ✅ **已修复（2026-09-02）**：两处改 `ui/`，镜像同步升 net10，`MAI_CONFIG` 改 `MAI_FILE` |
| D2 | `ConfigureOpenTelemetryModule` 曾对 `OTLP.Trace/Metrics` **无条件 `new Uri(...)`**，而 entrypoint/compose 对 OTLP 默认留空 | 曾导致**默认 `docker-compose up` 后端启动即抛异常**（[@DEP-S4](./bdd.md#dep-s4)） | ✅ **已修复（2026-09-02）**：新增 `ParseOtlpEndpoint`，空值/非法地址跳过导出，OTLP 变为可选项 |
| D3 | ~~entrypoint 只生成 `Storage.LocalPath`~~（该字段在 `SystemOptionStorage` 中**不存在**，S3 五字段全空 → S3 客户端无 ServiceURL，业务接口 500） | 曾导致容器形态上传类接口必 500（[@DEP-S3](./bdd.md#dep-s3)） | ✅ **已修复（2026-09-03）**：entrypoint 改为从 `S3_ENDPOINT/S3_BUCKET/S3_ACCESS_KEY_ID/S3_ACCESS_KEY_SECRET/S3_FORCE_PATH_STYLE` 生成 S3 段；compose 与 .env.example 同步透传。**部署注意**：`S3_ENDPOINT` 是单端点设计——预签名 URL 的 host 即应用所配端点，必须**同时**对应用与上传客户端可达（本地容器冒烟的验证方式：审计脚本进容器跑，见 SOP 第 6 节） |
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
