# 内网 Gitea Actions：手动构建并部署（OCI registry + SSH）

> 适用：源码在公网 Git，构建与部署走内网 Gitea Actions；构建机推镜像到内网 registry，
> 部署机经 SSH 拉取并切换容器。与 `mo_platform` 的 `.gitea/workflows/build-image.yml` **同构**，
> 仓库变量完全同名，可直接复用（含组织级变量）。
> 关联：[Docker 部署](./docker.md) ｜ [SOP](./sop.md) ｜ 落地文件：`.gitea/workflows/deploy.yml`、`deploy/gitea/remote-deploy.sh`

> ⚠️ 本仓库是公开仓库：**任何真实主机名、IP、端口、域名、用户名、指纹、凭据都不能写进被跟踪的文件**。
> 本文一律用 `<占位符>` 表示，实际值只放在 Gitea 仓库变量/密钥，以及部署机本地的 `DEPLOY.local.md`（不跟踪）。

## 1. 链路总览

```
公网 Git 仓库 ──(镜像同步/定期)──> 内网 Gitea <gitea-host>
                                      │ 手动 Run workflow（可选分支）
                                      ▼
                        act_runner「linux_local」（内网构建机）
                        buildx 构建前后端一体镜像
                        push ──> <registry>/moai:<sha>|:latest
                                      │
                                      │ SSH（MINI_SSH_RSA）
                                      ▼
                        <deploy-host>:<ssh-port>（部署机）
                        docker pull → 复用共享网络 → postgres-init 建库 / rustfs-init 建桶
                        → docker compose up -d moai → 端口探活失败自动回滚
```

与 mo_platform 的差异：MoPlatform 部署时重跑 `deploy-compose.sh` 重建配置；MoAI 只切镜像，
`configs/system.json` 与 `.env` 是运维资产，不被流水线覆盖。

## 2. 仓库变量（与 mo_platform 同名，必填项缺失会直接失败）

| 类型 | 名称 | 说明 |
|---|---|---|
| Variable/Secret | `ZOT_REGISTRY` | 构建机可访问的 registry `host:port`（纯 HTTP 也可以） |
| Variable/Secret | `ZOT_REGISTRY_URL` | 同上完整 URL（备用/排查） |
| Variable/Secret | `ZOT_USERNAME` | registry 用户名 |
| Variable/Secret | `ZOT_PASSWORD` | registry 密码 / token |
| Variable/Secret | `MINI_SSH_USER` | 部署机 SSH 用户 |
| Variable/Secret | `MINI_SSH_RSA` | 部署机 SSH 私钥全文 |
| Variable/Secret | `MINI_SSH_HOST` | 部署机地址 |
| Variable/Secret | `MINI_SSH_PORT` | 部署机 SSH 端口 |

可选项（不配走默认）：

| 变量 | 默认 | 说明 |
|---|---|---|
| `ZOT_REGISTRY_TARGET` | 同 `ZOT_REGISTRY` | **部署机视角**的 registry；部署机与构建机看到的地址不同时（NAT/VPN/Tailnet）在这里覆盖 |
| `MINI_SSH_KNOWN_HOST` | 空 | 目标机 known_hosts 行；配置后启用 `StrictHostKeyChecking=yes`，留空则 `accept-new` |
| `MOAI_APP_DIR` | `moai` | 部署机目录，相对 `$HOME` → `~/moai` |

> 同名变量可放 Gitea **组织级**一次给多个仓库用；仓库级优先。
> `MINI_SSH_RSA` 建议放 Secrets。工作流不会把变量值打印到日志。

## 3. Gitea 侧开关与 runner

`config/app.ini`：

```ini
[actions]
ENABLED = true
; 内网访问不到 github.com 时指向可用镜像（如 https://gitea.com）
DEFAULT_ACTIONS_URL = https://github.com

[repository]
; 必须包含 repo.actions
DEFAULT_REPO_UNITS = repo.code,repo.releases,repo.issues,repo.pulls,repo.wiki,repo.projects,repo.packages,repo.actions
```

runner 标签必须是 **`linux_local`**（与 mo_platform 一致）：

```bash
act_runner register --no-interactive \
  --instance http://<gitea-host> \
  --token <REGISTRATION_TOKEN> \
  --name moai-runner \
  --labels linux_local
```

label 不带 `:docker://` 即 host 模式；容器模式也能跑（工作流的 SSH 步骤只依赖宿主 `docker` 与 `/usr/bin/ssh`）。

构建机需要：Docker + buildx、`docker.m.daocloud.io` 可达（Docker Hub 基础镜像镜像）、npm/NuGet/`mcr.microsoft.com` 直连、可推 `<registry>`、可 SSH 到 `<deploy-host>:<ssh-port>`。

## 4. 部署机准备（一次性）

工作流会通过 SSH 同步 `docker-compose.yml` / `docker-compose.reuse.yml` / `init-pgvector.sql` /
`deploy/deploy-compose.sh` / `deploy/opensandbox/sandbox.toml` / `.env.example`，
**不覆盖 `.env` 与 `configs/`**。所以只需准备一次配置：

```bash
ssh -p <ssh-port> <deploy-user>@<deploy-host>
mkdir -p ~/moai/{configs/plugins,deploy/opensandbox} && cd ~/moai

# 首次目录为空时，从仓库拷一次 compose 相关文件（之后由流水线同步）：
#   docker-compose.yml docker-compose.reuse.yml init-pgvector.sql
#   deploy/deploy-compose.sh deploy/opensandbox/sandbox.toml .env.example

cp .env.example .env
vi .env
# 必改：
#   MOAI_SERVER_URL=https://<moai-domain>
#   S3_ENDPOINT=https://<oss-domain>      （预签名地址，须浏览器可达）
#   MOAI_PORT=8080                        （与 Caddy / 反代一致）
#   POSTGRES_* / RABBITMQ_* / S3_ACCESS_* / MOAI_AES_KEY
#   RUSTFS_CORS_ALLOWED_ORIGINS=https://<moai-domain>

bash deploy/deploy-compose.sh   # 生成 configs/system.json + 建库建桶 + 起容器
```

- 部署用户需在 `docker` 组，且能写 `~/moai`（脚本用 `sed` 改 `.env`）。
- registry 为纯 HTTP 时无需手工改 daemon：**脚本每次幂等写入 `insecure-registries` 并 `SIGHUP` 重载**。
- `.env` 的 `MOAI_IMAGE` 由流水线改写为 `<ZOT_REGISTRY_TARGET>/moai:<tag>`，原值备份到 `.env.bak.<时间戳>`。
- 端口不漂移：`MOAI_PORT` 为空时沿用现有 `moai` 容器的宿主端口。
- 库结构升级靠应用启动时的幂等 DDL 补齐（`EnsureCreated` + 后加表/列自愈）；空库首启会建全量表并写入种子 admin。

### 4.1 与其它服务共享基础设施时

若目标机上已有别的 Mo 系列服务（如 MoPlatform）用同名网络创建了 PostgreSQL/Redis/RabbitMQ/RustFS：

- **复用它们的实例，不另起一套**：同 PostgreSQL 不同库、同 Redis 不同键前缀、同 RabbitMQ 不同队列、同 RustFS 不同桶。
- `.env` 的 `POSTGRES_PASSWORD` / `RABBITMQ_USER` / `RABBITMQ_PASSWORD` / `S3_ACCESS_KEY_ID` / `S3_ACCESS_KEY_SECRET` 必须与共享实例一致（从对方的 `.env` 取），否则初始化会连不上。
- **共享 RustFS 的 CORS 必须放行本服务的站点域名**（对方 `.env` 的 `RUSTFS_CORS_ALLOWED_ORIGINS` 追加后重建该容器），否则浏览器直传被拦。
- 共享实例重装/换凭据后，要同步更新 `~/moai/.env` 并重跑一次 `deploy-compose.sh`。

## 5. 自动复用 moai 网络

`deploy/gitea/remote-deploy.sh` 与 `deploy/deploy-compose.sh` 同口径探测：

| 探测结果 | 使用文件 | 行为 |
|---|---|---|
| 有 `moai` 网络且 postgres/redis/rabbitmq/rustfs 健康 | `docker-compose.reuse.yml` | **不新建**这四类服务，只按容器名复用；仍幂等跑 `postgres-init`（建库 + `vector`/`uuid-ossp`/`pg_trgm`）与 `rustfs-init`（建桶），再重建 `moai` 容器 |
| 有 `moai` 网络但基础服务找不到 | — | 报错退出，提示先起共享基础设施（绝不另起第二套 Redis） |
| 没有 `moai` 网络 | `docker-compose.yml` | 起完整栈 |

共享容器名运行时反查（compose service label + `moai` 网络，兜底 `moai-<service>`/`mopl-<service>`），
自动导出 `MOAI_POSTGRES_HOST` 等，`.env` 不用填。

## 6. 触发与参数

仓库 → Actions → `deploy-moai` → Run workflow（先切换分支再点运行）。

| 参数 | 用途 |
|---|---|
| `image_tag` | 指定镜像 tag（留空 = `sha-<7位>`）；发布版本可填 `1.0.0` |
| `deploy_only` | `true` 时跳过构建，直接部署 registry 上已有的 tag（默认 `latest`，用于回滚/重放） |

镜像规则：构建推 `:<tag>` 与 `:latest`，目标机使用 `:<tag>`；回滚用 `deploy_only=true` + `image_tag=<上一个 sha>`。

## 7. 排障

| 现象 | 原因 | 处理 |
|---|---|---|
| 作业开头直接失败「缺少仓库变量/密钥」 | 变量未配或放错层级 | 按第 2 节补齐，组织级/仓库级都行 |
| 工作流列表为空 / 无 Run workflow | Actions 未开、缺 `repo.actions`、runner 未注册或 Gitea 版本过低 | 检查 `app.ini`、站点管理 → Actions 的 runner 在线状态 |
| 作业一直 queued | 没有 `linux_local` 标签的 runner | `--labels linux_local` 注册，或改 `runs-on` 与之匹配 |
| 构建卡在拉基础镜像 | Docker Hub 出口受限 | 确认 buildkitd 配置里的 `docker.m.daocloud.io` 镜像可用 |
| push 报 `http: server gave HTTP response to HTTPS client` | registry 纯 HTTP 未声明 | buildkitd 配置已含 `[registry."<host>"].http = true / insecure = true`；确认 `ZOT_REGISTRY` 与实际一致 |
| 目标机 pull 报 401 | registry 凭据错误 | 检查 `ZOT_USERNAME`/`ZOT_PASSWORD`；脚本用临时 `DOCKER_CONFIG`，不污染 `~/.docker` |
| 目标机 pull 报 `http: server gave HTTP response` | 部署机侧 registry 未声明 insecure | 用 `ZOT_REGISTRY_TARGET` 指定部署机视角地址；脚本会自动写 `daemon.json` 并 reload |
| `moai 网络存在但找不到 redis 容器` | 共享基础设施未启动或未加入 `moai` 网络 | 先起共享实例；`docker network inspect moai` 核对 |
| 部署后端口从 8080 变 8081 | `.env` 明确写了 `MOAI_PORT=8081` | 脚本只在为空时沿用旧端口，改成目标端口后重跑 |
| 健康检查失败已回滚 | 应用启动异常（配置/迁移/连不上中间件） | 看作业日志里的 `docker compose logs --tail=100 moai` |
| 上传 CORS 报错 | 共享 RustFS 未放行站点域名 | 见 4.1，追加域名并重建 RustFS |
