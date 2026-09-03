# 部署与本地环境（Deployment）设计规格（SDD）

> 关联：[SDD](./sdd.md) ｜ [BDD](./bdd.md) ｜ [TDD](./tdd.md) ｜ [SOP](./sop.md) ｜ 上游：[../infra/sdd.md](../infra/sdd.md)（SystemOptions/配置加载链） ｜ 证据：docker compose 命令（见 [TDD](./tdd.md)）
> 规范：[../DOC-STANDARD.md](../DOC-STANDARD.md)。行为场景见 BDD（@DEP-Sxx），本文不重复。

## 目标与形态

| 形态 | 组成 | 状态 |
|---|---|---|
| Docker Compose 一键部署 | pgvector(pg16) + redis7 + rabbitmq3 + moai 后端镜像（含前端静态资源） | ✅ D1/D2 已修复（2026-09-02，见已知缺陷表）；完整构建/起容器验收（[@DEP-S2](./bdd.md#dep-s2)）待执行 |
| 本地开发 | 后端 `dotnet run`（MAI_FILE 注入）+ 前端 `npm run dev` + 四个基础设施容器 | ✅ 当前实际可用形态（[@DEP-S12](./bdd.md#dep-s12)） |

覆盖对象：`Dockerfile`、`docker-entrypoint.sh`、`docker-compose.yml`、`.env.example`、`init-pgvector.sql`、`configs/system.json` 回退链。

## 配置注入链（核心设计）

- **容器形态**：`.env` → compose 环境变量 → `docker-entrypoint.sh` 用 heredoc 生成 `/app/configs/system.json` → 后端启动。工作目录 `/app`，`MAI_FILE` 未设置时加载器回退到 `configs/system.json`（相对 ContentRoot），容器内无需 MAI_FILE（[@DEP-S10](./bdd.md#dep-s10)）。
- **本地开发形态**：`MAI_FILE=<绝对路径>/system.local.json` 显式注入，绕过仓库内过时的 `configs/system.json`（Port=5000 且为 Windows 路径）（[@DEP-S11](./bdd.md#dep-s11)）。
- as-built 记录：Dockerfile 曾用无效变量 `ENV MAI_CONFIG=...`（加载器识别的是 `MAI_FILE`），**2026-09-02 已修正为 `ENV MAI_FILE=/app/configs/system.json`**，现与回退链双保险。

## Dockerfile（三阶段）

1. `frontend-builder`（node:22-slim）：`COPY ui/package*.json` → `npm ci` → 复制源码 → 删 lock 重装（绕 Rollup 可选依赖问题）→ `npm run build`。（原 `COPY ui/moai/...` 路径缺陷 D1，已修复）
2. `backend-builder`（**sdk:10.0**，仓库 TargetFramework 已升 net10.0）：复制 `Directory.Packages.props` + `Directory.Build.props` + `src/` → restore/build/publish `src/MoAI/MoAI.csproj`。
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
| D3 | 容器形态 `Storage.LocalPath=/app/files`（本地盘），本地开发形态用 S3/MinIO | 两形态存储行为不一致；容器上传的文件不在对象存储（[@DEP-S3](./bdd.md#dep-s3)） | 按环境选型；统一 S3 需改 entrypoint 生成 Storage.S3 段 |
| D4 | `.env.example` 的 OTLP 示例指 `127.0.0.1:4012`，容器内 `127.0.0.1` 是容器自身 | 照抄示例则 OTLP 上报失败（[@DEP-S6](./bdd.md#dep-s6)） | 写 collector 的容器网络名或宿主机地址 |
| D5 | README 引用的 `moai_docs/` 目录已不存在于仓库 | README 图片裂图 | 与部署无关，仅记录 |

## 本地开发环境（当前实际形态）

- 基础设施容器（自定义端口）：`moai-postgres` 5432、`moai-redis` 55379、`moai-rabbitmq` 55672（管理台 15673）、`moai-minio` 9000/9001（桶 `moai`，公共读）。
- 后端：`src/MoAI` 下 `MAI_FILE=<path>/system.local.json ASPNETCORE_ENVIRONMENT=Development dotnet run`，监听 5210/5211（Kestrel 双端口：`MoAI:Port` 与 Port+1）。
- 前端：`ui/` 下 `npm run dev`（4000），`VITE_ServerUrl=http://127.0.0.1:5210`。
- 种子账号 admin/abcd123456（root，id=1）；pgvector 扩展由首次建库 SQL 或应用 EnsureCreated 双保险保证（见 [../database-scaffold/sdd.md](../database-scaffold/sdd.md)）。
