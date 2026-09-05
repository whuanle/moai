# 部署与本地环境（Deployment）操作手册（SOP）

> 关联：[SDD](./sdd.md) ｜ [BDD](./bdd.md)（验收场景编号） ｜ [TDD](./tdd.md)（验证映射与回归命令） ｜ [SOP](./sop.md)。缺陷编号 D1~D5 见 [SDD 已知缺陷表](./sdd.md)。

## 1. 当前可用形态：本地开发环境

```bash
# 基础设施（compose 默认端口；本地实际容器为自定义端口的历史实例）
docker compose up -d postgres redis rabbitmq

# 后端（5210）
cd src/MoAI
MAI_FILE=/Users/wen/project/maomi/local-dev/system.local.json ASPNETCORE_ENVIRONMENT=Development dotnet run

# 前端（4000）
cd ui && npm run dev
```

种子账号：admin / abcd123456（root）。验收场景：[@DEP-S12](./bdd.md#dep-s12)。

## 2. Docker Compose 部署（✅ 2026-09-03 容器实测通过）

D1（路径/镜像/MAI_FILE）、D2（OTLP 空值容错）、D3（entrypoint 生成 S3 段）均已修复并实测，见 [TDD 修复记录](./tdd.md)。`.env` 需配置：`MOAI_SERVER_URL/MOAI_WEBUI_URL`（实际访问地址）、`MOAI_AES_KEY`（随机串）、**`S3_*` 五项（必配，存储为纯 S3 实现，不配则上传接口 500）**；OTLP 可选（勿照抄 `.env.example` 的 127.0.0.1:4012，见 D4，[@DEP-S6](./bdd.md#dep-s6)）。

```bash
cp .env.example .env && vim .env
docker compose up -d
docker compose logs -f moai         # 确认启动无异常
curl http://localhost:8080/api/common/serverinfo   # 冒烟
```

## 3. 升级 / 回滚

```bash
git pull && docker compose build moai && docker compose up -d moai
# 回滚：镜像按 tag 固定后 down/up 对应 tag；数据在 named volume，不受影响
```

## 4. 备份与恢复

```bash
docker exec moai-postgres pg_dump -U postgres moai > moai-$(date +%F).sql
# 恢复：cat moai-2026-09-02.sql | docker exec -i moai-postgres psql -U postgres -d moai
# 上传文件（本地开发形态在 MinIO 桶 moai；容器形态在 moai_files volume）
docker run --rm -v moai_files:/data -v $PWD:/backup alpine tar czf /backup/moai-files.tgz /data
```

## 5. 排障

| 现象 | 原因 | 处理 |
|---|---|---|
| docker build 在 COPY ui/moai 失败 | 历史缺陷 D1（**已修复**，现路径为 ui/） | 若复现说明镜像基线过旧，核对 Dockerfile（[@DEP-S1](./bdd.md#dep-s1)） |
| moai 容器反复重启，日志含 Uri/FormatException | 历史缺陷 D2（**已修复**，空值自动跳过） | 检查是否运行旧镜像；新代码 OTLP 未配置时不再抛异常（[@DEP-S4](./bdd.md#dep-s4)） |
| 容器上传文件不在对象存储 | 历史缺陷 D3（**已修复**：entrypoint 生成 S3 段） | `.env` 配 S3_* 五项；预签名 host 须同时被应用与客户端可达（见 sdd 缺陷表） |
| 容器启动后自行退出，日志 `ACCESS_REFUSED`（RabbitMQ PLAIN） | MQ 凭据与 broker 不符（默认 guest/guest 常被拒） | 传 `RABBITMQ_USER/RABBITMQ_PASSWORD`（Maomi.MQ 消费者失败会 StopHost） |
| 启动日志 `Cannot load library libgssapi_krb5.so.2` | 缺陷 D6：aspnet:10.0 无 Kerberos 库 | 实测不影响功能，忽略；要消除需 final 加装 libgssapi-krb5-2 |
| Apple Silicon `docker build` 报 SIGSEGV/MSB4184（amd64） | QEMU 仿真伪故障 | 用原生 arm64 构建（默认平台）；见 sdd「Apple Silicon 注意事项」 |
| 后端起在 5000 报地址占用 | 回退加载了过时 configs/system.json（macOS 5000 被 AirPlay 占用） | 显式设置 MAI_FILE（[@DEP-S11](./bdd.md#dep-s11)） |
| 前端 4000 请求 401/跨域 | VITE_ServerUrl 与后端端口不一致 | 检查 ui/.env.local |
| 前端代理 /openapi 拉不到 | vite.config.ts 代理 target 写死 5000（遗留） | syncapi 时显式传 5210 地址 |
| 登录报 RSA/加密错误 | system.local.json 与后端实际加载文件不一致 | 用 /api/common/serverinfo 核对 serviceUrl |

## 6. 验收流程（形态变更/部署物料修改后）

1. `docker compose config -q` 通过（[@DEP-S7](./bdd.md#dep-s7) 语法级）。
2. 后端本地启动 + `/api/common/serverinfo` 冒烟 200（[@DEP-S11](./bdd.md#dep-s11)）。
3. [local-dev/user-management-e2e.mjs](../../local-dev/user-management-e2e.mjs)（后端 5210 运行中）全绿。
4. 前端 `npm run typecheck && npm run lint && npm run test` 全绿。
5. （修复 D1 后新增）`docker compose build moai` 成功并起容器，页面可登录（[@DEP-S2](./bdd.md#dep-s2)）。

## 7. 远端部署实例：154 服务器（154.8.214.31）

> 2026-09-05 由本会话部署并完成全量测试。与宿主机上的 sms-stack 完全隔离（独立 compose 项目 `moai154`、独立网络/卷/容器名前缀）。

**实例信息**

| 项 | 值 |
|---|---|
| Web/API | http://154.8.214.31:18080（ufw 已放行，SPA + API 同端口） |
| MinIO S3 | http://154.8.214.31:19000（ufw 已放行，预签名直传需要客户端可达） |
| SSH | ubuntu@154.8.214.31（密码见 Obsidian SMS 台账，sudo 免密） |
| 部署目录 | `/home/ubuntu/moai154`（docker-compose.yml + app/ 运行时镜像构建上下文） |
| 容器 | moai154-app / -postgres(pgvector:pg16) / -redis / -rabbit / -minio，全部 mem_limit（合计 <900M，适配 1.9G 小机） |
| 种子账号 | admin / abcd123456（root） |

**低内存部署方式（服务器零构建）**：本地 `npm run build` + `dotnet publish -r linux-x64 --self-contained false`，产物 + dist(→wwwroot) + entrypoint 打包上传，服务器仅用 4 行 Dockerfile（FROM aspnet:10.0 + COPY）——2C/1G 可用内存跑不动 node/dotnet 构建。

**部署检查单（新机必做）**：① S3_* 五项 + RabbitMQ 正确凭据（错凭据 → 消费者异常 → StopHost 整机退出）；② **MinIO 桶 `moai` 预建**：`mc mb s/moai && mc anonymous set download s/moai`（缺桶 → PUT 404 NoSuchBucket → complate 409）；③ 防火墙放行 Web/MinIO 端口；④ entrypoint 可执行位。

**已验证（2026-09-05）**：serverinfo/SPA/静态对象 200；UM 34/34、团队 47/47、知识库 23/23、audit-345 14/14、锁定 8/8（脚本 REDIS_DEL_CMD 可覆盖后远端清 key）、存储 7/7（审计进容器，详见第 8 节）。

**两条环境硬限制（排障必读）**：
- **云主机公网 IP hairpin 不通**：服务器本机/容器内访问 `154.8.214.31:*` 一律超时；服务器侧验证走 `127.0.0.1` 或 compose 内网名。真实浏览器（公网）可达即可，服务器自测需换内网视角。
- **Mac→154 间歇 PMTU 黑洞**：直连 HTTP 会随机 stall/CONNECT_TIMEOUT（SSH 正常）。Mac 侧测试优先 SSH 隧道（`ssh -N -L 18081:127.0.0.1:18080`）；存储审计用"审计进容器"模式（服务器上 `docker run --network moai154_moai154 node:22-slim` 跑脚本）。

## 8. 历史验收存档（L3 证据，保留原始记录）

- **2026-09-05（154 服务器部署 + 全量测试 ✅）**：发现四台候选机均无 MoAI 部署后，在 154.8.214.31 从零部署（低内存方案，见第 7 节）。测试记录：团队 47/47、知识库 23/23 从 Mac 公网直连通过；UM 34/34、audit-345 14/14、锁定 8/8 经 SSH 隧道通过（期间发现锁定脚本的清 key 写死本地容器名 → 已改为 REDIS_DEL_CMD 可覆盖；重复运行导致的账号锁定属测试污染非缺陷，清 `moai:login:fail:*` 后恢复）；存储 7/7 在服务器容器内通过（过程中实证：新 MinIO 缺桶 → PUT 404 NoSuchBucket → complate 409，建桶 `mc mb s/moai` 后全绿；公网 IP hairpin 不通 → 审计须走内网视角；调试残留的未完成文件行走 /static 返回 500 而非 404，已登记为观察项）。
- **2026-09-03（@DEP-S2 容器全链路验收 ✅）**：`docker build -t moai:dep-s2-verify .`（arm64 原生）成功；`docker run` 起容器（S3_*/RABBITMQ_* 指向宿主基础设施，OTLP 留空）→ `serverinfo` 200、SPA `/` 与 `/login` 均 200；宿主跑 [user-management-e2e.mjs](../../local-dev/user-management-e2e.mjs) `http://127.0.0.1:8081` **34/34**；存储审计进容器跑 `docker run --rm -v ./local-dev:/audit -w /audit node:22-slim node audit-storage.mjs http://host.docker.internal:8081` **7/7**（预签名视角一致，直传 MinIO→完成→匿名 /static→秒传→校验→404）。过程中实证：OTLP 留空不崩（D2）；`ACCESS_REFUSED` 未传 MQ 凭据会 StopHost（排障表已录）；SigV4 预签名 URL 重映射 host 会 403（审计脚本注释已录）。
- **2026-09-02（轮 21，as-built 回溯）**：物料（Dockerfile/entrypoint/compose/.env.example/init-pgvector.sql）随仓库确认；`ls ui/moai` 证实 D1（目录不存在）；`grep new Uri` 证实 D2（45/58 行无条件）；`docker compose config -q` OK；`docker ps` 四基础设施容器 Up(healthy)；`curl /api/common/serverinfo` 返回 serviceUrl=5210 与 MAI_FILE 一致。容器形态整体验收**未通过**（被 D1/D2 阻断），待修复清单见 [TDD 待修复](./tdd.md)。

## 8. 变更记录

| 日期 | 变更 |
|---|---|
| 2026-09-02 | 初版（回溯整理），记录缺陷 D1~D5 |
| 2026-09-02 | 按 [DOC-STANDARD](../DOC-STANDARD.md) 重构：场景编号化（@DEP-S1~S12）、四件互链、职责瘦身；D1/D2 更正为 Dockerfile 两处（实测 grep） |
| 2026-09-02 | D1/D2 修复同步（代码复核：ui/ 路径 + net10 镜像 + MAI_FILE；ParseOtlpEndpoint 空值容错）；部署章节解除阻断，@DEP-S2 整体构建验收待执行 |
| 2026-09-03 | **@DEP-S2 全链路验收通过**：D3 修复（entrypoint S3 段 + compose/.env.example 透传）；apt 死重移除（依赖无 node-gyp）；新增 D6（krb5 探测告警）与 Apple Silicon 构建注意事项；排障表 +3 行（MQ 凭据 StopHost / krb5 / QEMU 仿真） |
| 2026-09-05 | 新增第 7 节「远端部署实例：154 服务器」（部署信息、检查单含 MinIO 建桶、hairpin/PMTU 两条环境硬限制）+ 存档 154 全量测试记录；锁定脚本清 key 命令改为 REDIS_DEL_CMD 可覆盖 |
