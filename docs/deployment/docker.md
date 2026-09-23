# MoAI Docker 部署文档

> 适用：Docker / Docker Compose 部署 MoAI（前后端一体镜像）。
> 关联：[SDD](./sdd.md) ｜ [BDD](./bdd.md) ｜ [TDD](./tdd.md) ｜ [SOP](./sop.md) ｜ [文档标准](../DOC-STANDARD.md)

## 1. 部署形态

MoAI 采用**前后端一体镜像**：前端编译为静态资源放入后端 `wwwroot`，由 .NET 后端同源托管，**不需要前后端分离部署**。

| 形态 | 部署内容 | 依赖 |
|---|---|---|
| **A. Docker 单容器** | 仅 `moai` 镜像（前端 + 后端） | 外部 PostgreSQL(pgvector)、Redis、RabbitMQ、OSS(S3)、OpenSandbox（可选） |
| **B. Docker Compose 一体** | `moai` + `postgres` + `redis` + `rabbitmq` + `rustfs` + `opensandbox-server` | 全部由 compose 提供，开箱即用 |

两种形态的**唯一差异是外部依赖从哪来**；`moai` 容器本身完全一致，配置统一通过 `configs/system.json` 注入。

> 图数据库（Memgraph / Neo4j）**不内置**。知识图谱模块为可选能力，需要时按 [第 7 节](#7-知识图谱可选接入-memgraph) 单独部署并接入。

## 2. 前置条件

- Docker Engine ≥ 20.10、Docker Compose v2（形态 B）
- 已放行端口：`8080`（MoAI）、`9000/9001`（RustFS，形态 B）、`18123`（OpenSandbox，形态 B）
- 内存建议 ≥ 2 GB（含 RustFS 与 OpenSandbox）；沙箱运行时另需 Docker 可用磁盘空间

## 3. 配置生成：`.env` → `configs/system.json`（必读）

Compose 部署**以 `.env` 为唯一配置来源**，由 `deploy/deploy-compose.sh` 生成应用配置并挂载进容器，**无需手写 `system.json`**：

- 生成路径 = `.env` 的 `MOAI_CONFIG_FILE`（默认 `./configs/system.json`，可填绝对路径）；每次生成前把旧文件备份为 `<路径>.bak`。
- `docker-compose.yml` 挂载 `${MOAI_CONFIG_FILE:-./configs/system.json}:/app/configs/system.json:ro`；后端通过 `MAI_FILE`（默认 `/app/configs/system.json`）加载。
- 仓库内 `configs/system.example.json` 是参考模板，也是镜像内置兜底（无挂载时复制为 `/app/configs/system.json.template`）。

`.env` 中与配置相关的变量：

| 变量 | 作用 | 默认 |
|---|---|---|
| `MOAI_SERVER_URL` | MoAI 对外访问地址（**含协议**，如 `https://moai.example.com`），写入 `Server`/`WebUI` | 留空自动探测本机 IP → `http://<IP>:<MOAI_PORT>` |
| `MOAI_PORT` | MoAI 容器映射到宿主机的端口（也用于自动探测时拼接） | `8080` |
| `MOAI_AES_KEY` | AES 加密密钥 | `please-change-this-aes-key` |
| `POSTGRES_*` / `RABBITMQ_*` | 数据库 / MQ 连接（host 固定为 compose 服务名） | 见 `.env.example` |
| `S3_ENDPOINT` | 对象存储对外地址（**含协议**，自定义域名 / 外部 OSS）；留空用 `http://<自动探测IP>:${RUSTFS_PORT}` | 空 |
| `S3_BUCKET` / `S3_ACCESS_KEY_ID` / `S3_ACCESS_KEY_SECRET` | 桶与凭据 | `moai` / `moaiadmin` / `moaiadmin123` |
| `RUSTFS_PORT` | 内置 RustFS 对外端口 | `9000` |
| `OPENSANDBOX_*` | 沙箱镜像与超时 | 见 `.env.example` |
| `OTLP_*` | 可观测性（留空禁用） | 空 |

生成的配置形如：

```jsonc
{
  "MoAI": {
    "Port": 8080,
    "Server": "<MOAI_SERVER_URL>",          // 如 https://moai.example.com
    "WebUI":  "<MOAI_SERVER_URL>",
    "AES": "<MOAI_AES_KEY>",
    "Database": "Database=<POSTGRES_DB>;Host=postgres;Password=<POSTGRES_PASSWORD>;Port=5432;Username=<POSTGRES_USER>;Search Path=public",
    "Redis":    "redis:6379",
    "RabbitMQ": "amqp://<RABBITMQ_USER>:<RABBITMQ_PASSWORD>@rabbitmq:5672",
    "OpenSandBox": { "Address": "http://opensandbox-server:8090", "Image": "...", "TimeoutSeconds": 900, "RenewThresholdSeconds": 300 },
    "Storage": { "Endpoint": "<S3_ENDPOINT 或 http://<自动探测IP>:<RUSTFS_PORT>>", "ForcePathStyle": true, "Bucket": "...", "AccessKeyId": "...", "AccessKeySecret": "..." },
    "MaxUploadFileSize": 104857600,
    "OTLP": { "Trace": "", "Metrics": "", "Protocol": 0 }
  },
  "Serilog": { /* 固定日志配置 */ }
}
```

### 3.1 存储端点与访问地址（重要）

对象存储为**纯 S3 实现**，`Storage.Endpoint` 的 host 会直接出现在**预签名上传/下载 URL** 中，必须**同时被 MoAI 容器和浏览器（上传客户端）可达**；`Server` 同样会被前端用于拼接 `/static/{objectKey}` 访问地址。

- 默认：`deploy-compose.sh` 自动探测本机 IP，`Server = http://<本机IP>:8080`、`Endpoint = http://<本机IP>:9000` → 容器与浏览器都可达。
- 域名/HTTPS（Caddy 反代）：**必须显式**设 `MOAI_SERVER_URL=https://moai.example.com`（含协议，无端口则不加），否则会拼成 `http://域名:8080` 导致资源 404/跨域。
- 自定义 OSS：设 `S3_ENDPOINT=https://oss.example.com`（须容器能解析且浏览器可达；外部 OSS 直接填其 endpoint）。
- 不要写 `rustfs:9000`（浏览器解析不了）或 `127.0.0.1`（容器访问的是自身）。

### 3.2 单容器部署时的配置

形态 A 没有 `deploy-compose.sh`：复制 `configs/system.example.json` 为 `configs/system.json`，按外部服务改 host（数据库/Redis/MQ/OSS），或用 `-e MOAI_SERVER_URL` 注入对外地址；若依赖跑在同一宿主机上，容器内可用 `host.docker.internal`（Linux 需 `--add-host host.docker.internal:host-gateway`）。

### 3.3 浏览器直传跨域（CORS）

前端是**直传对象存储**（预签名 PUT），当 MoAI 站点与对象存储域名不同源时，浏览器会先发 OPTIONS 预检，要求对象存储返回 `Access-Control-Allow-Origin` 等头，否则报：

```
Access to fetch at 'https://<oss-domain>/...' from origin 'https://<moai-domain>' has been blocked by CORS policy
```

**方式一（推荐，RustFS 自带）**：给 RustFS 设允许源，重启即可。

```env
# .env（compose 会传给 RustFS 的 RUSTFS_CORS_ALLOWED_ORIGINS）
S3_CORS_ALLOWED_ORIGINS=https://moai.example.com
```

**方式二（对象存储前有 Caddy/nginx 反代时）**：在反代层放行预检并补响应头。Caddyfile 示例：

```caddyfile
moaioss.example.com {
    # 预检直接 204 返回，不转发给对象存储
    @preflight method OPTIONS
    handle @preflight {
        header Access-Control-Allow-Origin "*"
        header Access-Control-Allow-Methods "GET, PUT, HEAD, POST, OPTIONS"
        header Access-Control-Allow-Headers "Content-Type, Authorization, x-amz-*, X-Amz-*"
        header Access-Control-Max-Age "3600"
        header Vary "Origin"
        respond "" 204
    }

    # 实际请求也补上 CORS 头
    header Access-Control-Allow-Origin "*"
    header Access-Control-Expose-Headers "ETag, x-amz-request-id"
    header Vary "Origin"

    reverse_proxy rustfs:9000
}
```

> 预签名直传不带 cookie，`Access-Control-Allow-Origin: *` 即可；若要按源收紧，把 `*` 换成 `https://moai.example.com`（或 `{http.request.header.Origin}`）。`reverse_proxy` 默认保留 Host，SigV4 签名（`host` 参与签名）仍有效，勿改写 Host。

验证（应回显 `Access-Control-Allow-Origin`）：

```bash
curl -i -X OPTIONS "https://<oss-domain>/<bucket>/test.png" \
  -H "Origin: https://<moai-domain>" \
  -H "Access-Control-Request-Method: PUT" \
  -H "Access-Control-Request-Headers: content-type"
```

## 4. 形态 A：Docker 单容器部署

只需部署 `moai` 一个容器：

```bash
# 1) 准备配置：cp configs/system.example.json configs/system.json，
#    再把 Database/Redis/RabbitMQ/Storage/OpenSandBox 的 host 改成你的外部服务
#    （对外地址可用 -e MOAI_SERVER_URL 注入，也可直接写死）
# 2) 启动
docker run -d \
  --name moai \
  --restart unless-stopped \
  -p 8080:8080 \
  -e MAI_FILE=/app/configs/system.json \
  -e MOAI_SERVER_URL=https://moai.example.com \
  -e TZ=Asia/Shanghai \
  --add-host host.docker.internal:host-gateway \
  -v "$(pwd)/configs/system.json:/app/configs/system.json:ro" \
  -v moai_files:/app/files \
  whuanle/moai:latest
```

冒烟：

```bash
curl -fsS http://localhost:8080/api/common/serverinfo
# 浏览器打开 http://localhost:8080 （种子账号 admin / abcd123456）
```

## 5. 形态 B：Docker Compose 一体部署

包含：`postgres(pgvector/pg16)` + `redis` + `rabbitmq` + `rustfs`（替代 MinIO）+ `opensandbox-server` + `moai`。

> **数据库镜像硬约束**：必须用 `pgvector/pgvector:pg16`（或自带 pgvector 的镜像），**不能用官方 `postgres` 镜像**。MoAI 依赖 `CREATE EXTENSION vector`（见 `init-pgvector.sql`），裸 `postgres` 镜像无该扩展，建库即失败。形态 A 使用外部数据库时同样要求已安装 pgvector 扩展。

```bash
cp .env.example .env          # 按需修改账号/端口；MOAI_SERVER_URL 留空会自动探测
bash deploy/deploy-compose.sh # 按 .env 生成配置 → 预拉沙箱镜像 → 拉取/构建 → 启动
# 或手动（需已生成 configs/system.json）：
docker compose up -d
```

> 直接用 `docker compose up -d` 时不会自动探测，请在 `.env` 设置 `MOAI_SERVER_URL=<对外地址>`（如 `https://moai.example.com`）与 `S3_ENDPOINT`，否则回退 `http://localhost:8080` / `http://localhost:9000`。

访问：`http://<host>:8080`。RustFS 控制台：`http://<host>:9001`（账号见 `.env` 的 `S3_ACCESS_KEY_ID/S3_ACCESS_KEY_SECRET`）。

常用命令：

```bash
docker compose logs -f moai
docker compose ps
docker compose down            # 停止（保留数据卷）
docker compose down -v         # 停止并删除数据卷（危险）
```

### 5.1 RustFS 替代 MinIO

- 镜像 `rustfs/rustfs:latest`，S3 API `:9000`，控制台 `:9001`，凭据由 `RUSTFS_ACCESS_KEY/RUSTFS_SECRET_KEY` 指定（`.env` 的 `S3_*`）。
- `rustfs-init` 一次性容器等待 RustFS 就绪后幂等创建桶 `S3_BUCKET`（默认 `moai`）。
- 使用外部 OSS（阿里云 OSS / S3 / COS 等）时：在 `.env` 设 `S3_ENDPOINT` 与 `S3_*`，可从 compose 删除 `rustfs`/`rustfs-init`。

### 5.2 OpenSandbox 沙箱

**为什么单独一个镜像**：`opensandbox-server` 是 Python 服务且需要访问 Docker（通过 `docker.sock` 创建沙箱容器）。把它塞进 .NET 服务会导致 .NET 容器同时持有 Docker 控制能力，显著扩大容器逃逸面。因此拆分为独立容器，`.NET` 只通过 HTTP API 调用。

**官方镜像已提供（已验证）**，无需自行打包：

| 组件 | 镜像 | 说明 |
|---|---|---|
| 生命周期服务 | `opensandbox/server:latest` | 也可用 `ghcr.io/opensandbox-group/opensandbox/server` 或阿里云 `sandbox-registry.cn-zhangjiakou.cr.aliyuncs.com/opensandbox/server:release-1.1.0` |
| 沙箱运行时 | `opensandbox/code-interpreter:v1.1.0` | 由服务端按需创建，**部署时只拉取、不运行** |
| 沙箱执行/网络 | `opensandbox/execd:v1.1.0`、`opensandbox/egress:v1.1.7` | 由服务端运行时拉取，建议预拉 |

> 若无法访问上述仓库，可用兜底自打包：`docker build -t <your-registry>/opensandbox-server:latest deploy/opensandbox`，再设 `.env` 的 `OPENSANDBOX_SERVER_IMAGE`。

compose 中 `opensandbox-server` 的要点：

- 挂载 `/var/run/docker.sock`（创建沙箱容器）与 `deploy/opensandbox/sandbox.toml`（`SANDBOX_CONFIG_PATH`）。
- `extra_hosts: host.docker.internal:host-gateway` + `sandbox.toml` 的 `[docker] host_ip` / `[proxy] resolve_internal=false`：服务端在容器内时，返回宿主机已发布端口供 MoAI 直连沙箱。
- 未配置 `api_key` 时需 `OPENSANDBOX_INSECURE_SERVER=YES`（compose 默认已设）。生产建议在 `sandbox.toml` 设 `server.api_key`，并同步 `system.json` 的 `OpenSandBox.ApiKey`。
- MoAI 侧 `OpenSandBox.Address` 指向 `http://opensandbox-server:8090`（compose 内网）。

预拉取沙箱镜像（部署脚本已包含）：

```bash
docker compose --profile sandbox-images pull sandbox-image   # code-interpreter
docker pull opensandbox/execd:v1.1.0
docker pull opensandbox/egress:v1.1.7
```

## 6. 构建与发布镜像（Docker Hub）

发布脚本：`deploy/publish.sh`（默认推送 `whuanle/moai`）。

```bash
# 推送 whuanle/moai:<时间戳> 与 :latest
bash deploy/publish.sh

# 指定 tag / 多平台 / 仅本地构建
TAG=v1.0.0 bash deploy/publish.sh
PLATFORMS=linux/amd64,linux/arm64 bash deploy/publish.sh
PUSH=0 bash deploy/publish.sh
```

- 前置：目标机已 `docker login`（免密推送环境无需重复登录）。
- 默认平台 `linux/amd64`；多平台需 `buildx` + QEMU（Apple Silicon 上仿真 amd64 构建已知不稳定，见 [SOP 排障](./sop.md)）。
- 发布后，服务器 `docker compose pull moai && docker compose up -d moai` 即可升级。

## 7. 知识图谱（可选接入 Memgraph）

知识图谱模块不随 compose 内置。需要时单独部署图数据库并接入：

```bash
docker run -itd --name memgraph \
  -p 7687:7687 -p 17444:7444 \
  -v memgraph_data:/var/lib/memgraph \
  -e MEMGRAPH="--storage-snapshot-interval-sec=300 --storage-snapshot-on-exit=true" \
  memgraph/memgraph-mage
```

然后在 MoAI「系统设置 → 图数据库」中开启并填写：

- 连接地址：`bolt://<host>:7687`（MoAI 容器内访问宿主机用 `bolt://host.docker.internal:7687`，Linux 需给 moai 容器加 `--add-host host.docker.internal:host-gateway`）
- 用户名/密码：留空（Memgraph 社区版默认无鉴权）
- 方言：`memgraph`（接入外部 Neo4j 时选 `neo4j`）

> 社区版 Memgraph 默认纯内存，务必保留 `--storage-snapshot-interval-sec` 与 `--storage-snapshot-on-exit=true`，否则容器重启丢图。

## 8. 升级 / 回滚 / 备份

```bash
# 升级
docker compose pull moai && docker compose up -d moai
# 回滚：改用固定 tag 的镜像（如 whuanle/moai:v1.0.0）后 up -d

# 备份 PostgreSQL
docker exec moai-postgres pg_dump -U postgres moai > moai-$(date +%F).sql
# 备份对象存储（RustFS 数据卷）
docker run --rm -v moai_rustfs_data:/data -v "$PWD":/backup alpine tar czf /backup/rustfs-data.tgz /data
# 备份上传文件卷
docker run --rm -v moai_moai_files:/data -v "$PWD":/backup alpine tar czf /backup/moai-files.tgz /data
```

> 卷名前缀为 compose 项目名（`name: moai`），实际卷名为 `moai_rustfs_data`、`moai_moai_files` 等，可用 `docker volume ls` 核对。

## 9. 排障

| 现象 | 原因 | 处理 |
|---|---|---|
| 容器启动报 `$MAI_FILE 是目录` | 宿主机缺少 `configs/system.json`，docker 把挂载点建成了目录 | 补齐文件后重启；确认挂载为 `文件:文件` |
| 上传接口 500 | `Storage` 未配置或端点不可达 | 检查 `Storage` 五项；预签名 host 须浏览器可达（见 3.1） |
| 上传 404 NoSuchBucket | 桶未创建 | 确认 `rustfs-init` 成功；或手动建桶（控制台/`mc mb`） |
| 沙箱工具报「未配置沙箱服务地址」 | `OpenSandBox.Address` 为空 | 补配置并重启 |
| 沙箱长期 Pending | 沙箱镜像拉取失败 | 在宿主机预拉 `opensandbox/code-interpreter`；检查网络 |
| 沙箱创建后调用超时 | MoAI 容器无法回连宿主机端口 | 确认 moai 容器有 `host.docker.internal:host-gateway`；`sandbox.toml` 的 `resolve_internal=false` |
| `opensandbox-server` 启动即退出 | 未设 API Key 且未确认 | 设置 `OPENSANDBOX_INSECURE_SERVER=YES` 或配置 `api_key` |
| 知识图谱不可用 | 未部署图库或未开启 | 见第 7 节，并在系统设置开启 |
| 启动日志 `Cannot load library libgssapi_krb5.so.2` | aspnet:10.0 无 Kerberos 库 | 实测不影响功能，忽略 |

更多历史排障见 [SOP](./sop.md)。
