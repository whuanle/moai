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

## 2. Docker Compose 部署（D1/D2 已修复，整体构建验收待执行）

原阻断缺陷已于 2026-09-02 修复：**D1**（Dockerfile 路径 `ui/moai/`→`ui/`、镜像升 net10、`MAI_FILE` 环境变量修正，[@DEP-S1](./bdd.md#dep-s1)）；**D2**（OTLP 空值容错 `ParseOtlpEndpoint`，[@DEP-S4](./bdd.md#dep-s4)）。可选：`.env` 给 `OTLP_TRACE/OTLP_METRICS` 配可达端点开启上报（勿照抄 `.env.example` 的 127.0.0.1:4012，见 D4，[@DEP-S6](./bdd.md#dep-s6)；不配则自动跳过导出）。

```bash
cp .env.example .env && vim .env    # MOAI_SERVER_URL/MOAI_WEBUI_URL 改实际访问地址，AES 换随机串
docker compose up -d                # 首次拉镜像或本地 build
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
| 容器上传文件不在对象存储 | 缺陷 D3：容器形态 LocalPath | 按环境选型，见 SDD 已知缺陷表 |
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

## 7. 历史验收存档（L3 证据，保留原始记录）

- **2026-09-02（轮 21，as-built 回溯）**：物料（Dockerfile/entrypoint/compose/.env.example/init-pgvector.sql）随仓库确认；`ls ui/moai` 证实 D1（目录不存在）；`grep new Uri` 证实 D2（45/58 行无条件）；`docker compose config -q` OK；`docker ps` 四基础设施容器 Up(healthy)；`curl /api/common/serverinfo` 返回 serviceUrl=5210 与 MAI_FILE 一致。容器形态整体验收**未通过**（被 D1/D2 阻断），待修复清单见 [TDD 待修复](./tdd.md)。

## 8. 变更记录

| 日期 | 变更 |
|---|---|
| 2026-09-02 | 初版（回溯整理），记录缺陷 D1~D5 |
| 2026-09-02 | 按 [DOC-STANDARD](../DOC-STANDARD.md) 重构：场景编号化（@DEP-S1~S12）、四件互链、职责瘦身；D1/D2 更正为 Dockerfile 两处（实测 grep） |
| 2026-09-02 | D1/D2 修复同步（代码复核：ui/ 路径 + net10 镜像 + MAI_FILE；ParseOtlpEndpoint 空值容错）；部署章节解除阻断，@DEP-S2 整体构建验收待执行 |
