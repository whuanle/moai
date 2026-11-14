# 系统级接入（子系统+应用接入合并模型）运维手册（SOP）

> 关联：[SDD](./sdd.md) ｜ [BDD](./bdd.md) ｜ [TDD](./tdd.md) ｜ [SOP](./sop.md)
> 面向：平台管理员（root）与接入开发方（子系统/ERP 等）。验收流程见第 4 节。

## 1. 存量库升级（一次性）

新库（EnsureCreated 全新建库）无需任何操作。存量库需执行（幂等，可重复执行）：

```bash
docker exec -i moai-postgres psql -U postgres -d moai < asserts/access_app_merge_subsystem.sql
# 或本地直连（按开发库实际连接参数替换）
python -X utf8 - <<'EOF'
import psycopg2
sql = open('asserts/access_app_merge_subsystem.sql', encoding='utf-8').read()
conn = psycopg2.connect(host='<host>', port=5432, dbname='<db>', user='postgres', password='<pwd>')
conn.autocommit = True
conn.cursor().execute(sql)
EOF
```

脚本内容：access_app 补 `app_type/team_scope/callback_url` 列、name 加宽 50；subsystem 表行迁入（id 原样迁移，团队/组织授权引用不断）；系统级 scopes 归 0（资源范围分档下线）；删 subsystem 表。

- **2026-10-29 之前的库**：先执行 [asserts/access_app_unify.sql](../../asserts/access_app_unify.sql)（system_access_app 迁入 access_app），再执行上面的合并脚本。
- 不执行的典型症状：管理面 500 `42P01: relation "subsystem" does not exist`（旧实体已删，实为启动 EnsureCreated 新建 access_app 后旧表残留无碍，但存量子系统数据未迁移）。
- 本轮为手工同步实体/EF 配置（避免 PostgresScaffold 全量覆盖在途改动）；后续在该表加列请沿用 asserts 幂等 SQL + 手工同步模式。

## 2. 子系统开发方对接指引

1. **拿凭证**：root 在「管理 → 应用接入」新建接入，类型选**子系统**（自填 appid，如 `report`）；appid 创建时完整返回，密钥仅回显掩码（`xxxx****xxxx`），**随时可点「查看密钥」信封解密查看/复制**（飞书式；密钥丢失无需重置）。
2. **换 token**：`POST /api/subsys/token`（匿名）body `{"appId":"report","secret":"..."}` → `accessToken` + `refreshToken`（Bearer，默认 2h）。到期前 `POST /api/subsys/token/refresh {"refreshToken":"..."}` 轮换新对（7 天 refresh，免重发密钥；停用/删除后刷新即拒）。**子系统自填 appid 不能直连调用**（401），必须先换 token。
3. **心跳（多实例存活探针）**：每个实例启动时生成稳定 `instanceId`，`POST /api/subsys/heartbeat`，`Authorization: Bearer <token>`，body `{"instanceId":"...","version":"..."}`（instanceId 必填）；**每实例 ≤20s 一次**（判定窗口 60s，容忍 2 次丢失）。列表/自省回显在线状态与**在线实例数**；实例崩溃 60s 后自动掉出集合，优雅停机直接停跳即可。
4. **自省**：`GET /api/subsys/info` 校验 token 与自身概况（appid/类型/状态/团队范围/在线实例数）。
5. **授权语义（SSO 门禁）**：子系统默认全员可用；root 可切「指定团队」并维护团队/组织（角色+方向）授权——任一维度命中即可使用。
6. **委托 SSO 登录（用户登录子系统）**：
   - root 在编辑弹窗维护「登录回调地址列表」（可多个入口：测试/生产等，最多 10 个），未配置=不开放；
   - 子系统引导用户跳 `https://<moai>/sso/authorize?client=<appid>&redirect_uri=<可选，指定本次入口须在注册列表内>&state=<可选透传>`；缺省回跳注册列表第一个；
   - MoAI 侧自动完成登录→签发 60s 一次性委托码→回跳 `回调地址?code=...&state=...`；
   - 子系统后端（持机器 token）`POST /api/subsys/sso/exchange {code}` 兑换用户 access（2h）/refresh（7d）与用户资料；
   - 会话续期 `POST /api/subsys/sso/refresh {refreshToken}`；refresh 失效时重新引导用户走一遍 SSO 跳转。
7. **故障语义**：密钥错误 401；接入被停用 403（换 token/机器调用/心跳/委托登录与刷新一并拒绝）；被删除 401。遇到 403 先让 root 检查状态，再重置密钥。

## 3. 排障表

| 症状 | 原因 | 处置 |
|---|---|---|
| 管理接口 500 `relation "subsystem" does not exist` | 存量库未执行迁移 SQL | 第 1 节执行 asserts/access_app_merge_subsystem.sql |
| 换 token 401 | 密钥错/appid 错/接入已删除/换过密钥 | 核对 appid；root 重置密钥后用新密钥 |
| 换 token 403 | 接入已停用 | root 启用后重试 |
| 自填 appid 直连 401 | 直连轨仅限应用接入（moai-sa-） | 子系统先经 /api/subsys/token 换 token |
| 心跳 400 | 缺 instanceId | 每实例携带稳定实例标识 |
| 心跳 401/403 | token 过期或接入被删/停用 | 重换 token；仍失败查 root 列表确认存在 |
| 列表在线长期离线 | 心跳间隔 >60s 或未启动 | 检查心跳循环与网络；多实例时逐实例确认心跳 |
| 团队看不到子系统 | 未授权或子系统停用 | root 调整团队/组织授权或启用 |
| 委托码兑换 401 | 码过期（60s）/已使用/兑换方非签发对象 | 重新引导用户走 SSO 跳转获取新码 |
| 委托码签发 403/404 | 停用/未配置回调/用户未获授权；应用接入行不参与 SSO（404） | root 启用、配置回调或调整授权；确认对象是子系统行 |
| 刷新 401/403 | refresh 过期、接入停用或用户被禁用 | 重新 SSO；仍失败查 root 列表与用户状态 |
| 查看密钥 400 | 存量 PBKDF2 哈希行不可回显 | 重置密钥（重置后转为可查看格式） |
| 换取 token 429 | 换取失败触发限流：(IP, appid) 10 次/60s 或 IP 50 次/60s | 停止重试，60s 窗口自愈；核对 appid/密钥后再试 |

## 4. 验收流程（发版后执行）

```bash
# 后端运行中（默认 :5000）
node local-dev/subsystem-e2e.mjs                                 # 期望 PASS=90 FAIL=0（SUB-01~90，含心跳 v2 与合并模型 @SUB-S53~S62）
node local-dev/system-access-app-e2e.mjs http://127.0.0.1:5000   # 期望 58 通过 / 0 失败（@SAA，scope 分档用例已退役）
dotnet test tests/MoAI.Subsystem.Tests                           # 期望 40/40
cd ui && npm run typecheck && npx vitest run src/pages/admin/__tests__/AdminAccessApps.test.tsx src/pages/admin/__tests__/AdminIntegrations.test.tsx src/pages/teams/apps/__tests__/TeamAccessApps.test.tsx src/pages/sso/__tests__/SsoAuthorize.test.tsx  # 期望 19/19
```

浏览器走查（root 账号）：侧边栏「应用接入」→ 单列表（类型列区分子系统/应用接入）→ 新建子系统（自填 appid/回调/授权）与应用接入（即开即用）→ 一次性凭证弹窗 → 编辑回显 → 启停/重置/删除二次确认（对应 @SUB-S2、@SUB-S53~S55）。

## 5. 应用接入对接指引（系统级 appid+密钥，ERP/OA 等外部系统）

1. root 在「应用接入」新建，类型选**应用接入**：仅填名称/描述，appid（`moai-sa-` 前缀）创建时完整返回；密钥仅回显掩码，**随时可点「查看密钥」信封解密查看/复制**，立即交付对接系统即可。
2. 对接双轨（推荐 token 轨）：
   - 换取：`POST /api/subsys/token`（匿名）body `{"appId":"moai-sa-...","secret":"..."}` → access（默认 2h）+ refresh（7d）；续期 `POST /api/subsys/token/refresh`；
   - 直连：`Authorization: Bearer moai-sa-...` 直接调协作面（长期凭证，建议仅探测/低频场景）。
3. **全量资源访问**（资源范围分档已下线，无需选择范围）：
   - 用户治理：`POST /api/subsys/system/users`（**按 userName 幂等 upsert**，ERP 定时推送主入口；未带密码自动生成一次性初始密码；可选携带 `oauthProviderId/oauthSub/oauthUnionId` 幂等绑定第三方身份，响应 `oauthBound`）、`GET /users/list`、`GET /users/{id}`、`PUT /users/{id}/disable`（root 受保护）；
   - 内部应用访问用户 token：`POST /api/subsys/system/app-tokens`（`{appId, oauthProviderId, oauthSub, oauthUnionId?, nickName?}` → 按第三方绑定换算站内用户并颁发绑定单应用的 token；未绑定时在本次请求内自动建户并绑定，返回 `userCreated=true`，重复请求返回 `userCreated=false`）；
   - 组织：`GET|POST /api/subsys/system/org/roles`、`PUT|DELETE /org/roles/{id}`、`GET /org/roles/{id}/users`、`GET|PUT /org/users/{userId}/roles`；
   - 团队：`GET /api/subsys/system/teams/list`、`POST /teams`、`PUT /teams/{id}`、`POST /teams/{id}/users`（成员幂等 upsert）、`DELETE /teams/{id}/users/{userId}`（Owner 受保护）。
4. `GET /api/subsys/info` 自省（身份/类型/状态/在线实例数，可用性探测）；可选心跳接入同一 `/api/subsys/heartbeat`。
5. 停用后双轨凭据即时 403；删除后双轨与 refresh 均即时 401。变更即时生效。
