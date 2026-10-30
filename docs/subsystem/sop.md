# 子系统接入（Subsystem，SUB）运维手册（SOP）

> 关联：[SDD](./sdd.md) ｜ [BDD](./bdd.md) ｜ [TDD](./tdd.md) ｜ [SOP](./sop.md)
> 面向：平台管理员（root）与子系统开发方。验收流程见第 4 节。

## 1. 存量库升级（一次性）

新库（EnsureCreated 全新建库）无需任何操作。存量库需执行（幂等，可重复执行）：

```bash
docker exec -i moai-postgres psql -U postgres -d moai < asserts/subsystem.sql
# 或本地直连（按开发库实际连接参数替换）
python - <<'EOF'
import psycopg2
sql = open('asserts/subsystem.sql', encoding='utf-8').read()
conn = psycopg2.connect(host='<host>', port=5432, dbname='<db>', user='postgres', password='<pwd>')
conn.autocommit = True
conn.cursor().execute(sql)
EOF
```

执行后重跑 `tool/PostgresScaffold` 逆向生成保持库与实体一致（仓库 SOP）。不执行的典型症状：子系统相关接口 500 `42P01: relation "subsystem" does not exist`。

**应用接入统一表迁移（2026-10-29 起，团队/系统级同表）**：存量库另需执行 [asserts/access_app_unify.sql](../../asserts/access_app_unify.sql)——access_app 补列（level/key_prefix/secret/secret_salt/status）、system_access_app 迁入并 scope 位左移至高位、删旧表；不执行的典型症状：系统接入创建 500 `42P01: relation "system_access_app" does not exist`（列缺失则为 `42703: column "level" does not exist`）。

## 2. 子系统开发方对接指引

1. **拿凭证**：root 在「管理 → 子系统管理」创建子系统（自选 appid，如 `report`），**当场复制保存 48 位密钥**（只显示一次，丢失只能重置）。
2. **换 token**：`POST /api/subsys/token`（匿名）body `{"key":"report","secret":"..."}` → `accessToken` + `refreshToken`（Bearer，默认 2h）。到期前 `POST /api/subsys/token/refresh {"refreshToken":"..."}` 轮换新对（7 天 refresh，免重发密钥；停用/删除后刷新即拒）。
3. **心跳**：`POST /api/subsys/heartbeat`，`Authorization: Bearer <token>`，body 可带 `instanceId`/`version`；**每 ≤60s 一次**（在场键 TTL 60s，超时判离线）；优雅停机可停止心跳自然离线。
4. **自省**：`GET /api/subsys/info` 校验 token 与授权概况（appid/状态/团队范围/在线）。
5. **授权语义**：子系统默认全员可用；root 可切「指定团队」并维护授权清单——未授权团队的成员不会看到该子系统（能力面后续轮次接入后，调用也将按此门禁）。
6. **委托 SSO 登录（用户登录子系统）**：
   - root 在子系统管理页填写「登录回调地址」（如 `https://report.example.com/sso/callback`），未配置=不开放；
   - 子系统引导未登录用户跳 `https://<moai>/sso/authorize?client=<appid>&state=<可选透传>`；
   - MoAI 侧自动完成登录→签发 60s 一次性委托码→回跳 `回调地址?code=...&state=...`；
   - 子系统后端（持机器 token）`POST /api/subsys/sso/exchange {code}` 兑换用户 access（2h）/refresh（7d）与用户资料（userId/userName/nickName/avatarPath）；
   - 会话续期 `POST /api/subsys/sso/refresh {refreshToken}`；access 过期且 refresh 失效时重新引导用户走一遍 SSO 跳转即可。
7. **故障语义**：密钥错误 401；子系统被停用 403（换 token/机器调用/委托登录与刷新一并拒绝）；被删除 401。遇到 403 先让 root 检查状态，再重置密钥。

## 3. 排障表

| 症状 | 原因 | 处置 |
|---|---|---|
| 管理接口 500 `relation "subsystem" does not exist` | 存量库未执行升级 SQL | 第 1 节执行 asserts/subsystem.sql |
| 换 token 401 | 密钥错/appid 错/子系统已删除/换过密钥 | 核对 appid；root 重置密钥后用新密钥 |
| 换 token 403 `subsystem_disabled` | 子系统已停用 | root 启用后重试 |
| 心跳 401 `invalid_token` | token 过期或子系统被删 | 重换 token；仍失败查 root 列表确认存在 |
| 心跳 403 | 子系统被停用 | root 启用 |
| 列表在线状态长期离线 | 心跳间隔 >60s 或未启动 | 检查子系统心跳循环与网络 |
| 团队看不到子系统 | 未授权或子系统停用 | root 调整授权范围（全员/指定团队）或启用 |
| 委托码兑换 401 | 码过期（60s）/已使用/兑换方非签发对象 | 重新引导用户走 SSO 跳转获取新码 |
| 委托码签发 403 | 子系统停用/未配置回调/用户团队未授权 | root 启用、配置回调或调整授权 |
| 刷新 401/403 | refresh 过期、子系统停用或用户被禁用 | 重新 SSO；仍失败查 root 列表与用户状态 |

## 4. 验收流程（发版后执行）

```bash
# 后端运行中（默认 :5000）
node local-dev/subsystem-e2e.mjs                              # 期望 PASS=53 FAIL=0（E2E 检查 SUB-01~53，含机器 token 刷新 @SUB-S45）
node local-dev/system-access-app-e2e.mjs http://127.0.0.1:5000  # 期望 61 通过 / 0 失败（@SAA-S1~S59）
dotnet test tests/MoAI.Subsystem.Tests                        # 期望 31/31
cd ui && npm run typecheck && npx vitest run src/pages/admin/__tests__/AdminSubsystems.test.tsx src/pages/admin/__tests__/AdminIntegrations.test.tsx src/pages/teams/apps/__tests__/TeamAccessApps.test.tsx src/pages/sso/__tests__/SsoAuthorize.test.tsx  # 期望 19/19
```

浏览器走查（root 账号）：侧边栏「子系统管理」→ 新建（全员/指定团队两种）→ 一次性密钥弹窗 → 编辑回显 → 停用/重置/删除二次确认（对应 @SUB-S2、S27~S31）。

## 7. 系统应用接入（系统级 appid+密钥，ERP 等内部系统）

1. root 在管理后台「应用接入」Tab（/admin/integrations?tab=apps）新建：名称 + 资源范围（组织架构/用户治理/团队各三档）。
2. **appid 与密钥原文均仅创建时显示一次**（`moai-sa-` 前缀），立即交付对接系统；密钥遗失走「重置密钥」（旧密钥立即失效，新密钥仅显示一次）。
3. 对接双轨（推荐 token 轨）：
   - 换取：`POST /api/subsys/system/token`（匿名）body `{"appId":"moai-sa-...","secret":"..."}` → access（默认 2h）+ refresh（7d）；续期 `POST /api/subsys/system/token/refresh`；
   - 直连：`Authorization: Bearer moai-sa-...` 直接调协作面（长期凭证，建议仅探测/低频场景）。
4. 系统资源接口（均按 scope 分档，越权 403）：
   - 用户治理 user_read/user_write：`POST /api/subsys/system/users`（**按 userName 幂等 upsert**，ERP 定时推送主入口；未带密码自动生成一次性初始密码）、`GET /users/list`、`GET /users/{id}`、`PUT /users/{id}/disable`；
   - 组织角色 org_read/org_write：`GET|POST /org/roles`、`PUT|DELETE /org/roles/{id}`、`GET /org/roles/{id}/users`、`GET|PUT /org/users/{userId}/roles`；
   - 团队治理 team_read/team_write：`GET /teams/list`、`POST /teams`、`PUT /teams/{id}`、`POST /teams/{id}/users`（成员幂等 upsert，Admin/Member）、`DELETE /teams/{id}/users/{userId}`。
5. `GET /api/subsys/system/info` 自省（身份/范围/状态，可用性探测）。
6. 停用后双轨凭据即时 403；删除后双轨与 refresh 均即时 401。变更范围即时生效。
