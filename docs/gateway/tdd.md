# 团队网关模块验证映射（TDD）

> 关联：[SDD](./sdd.md) ｜ [BDD](./bdd.md) ｜ [TDD](./tdd.md) ｜ [SOP](./sop.md) ｜ 证据：[local-dev/team-apikey-scope-e2e.mjs](../../local-dev/team-apikey-scope-e2e.mjs)、[local-dev/gateway-e2e.mjs](../../local-dev/gateway-e2e.mjs)

## 映射表

| 场景 | 验证物 | 结果（日期） |
|---|---|---|
| @GW-S3 | local-dev/gateway-e2e.mjs（网关鉴权组：无 key/伪造/已下线 moai- 前缀 401、Bearer/x-api-key 200、路由团队 403、未授权模型 404、keys 端点 404） | GW 15/15（2026-09-27） |
| @GW-S6 | local-dev/team-apikey-scope-e2e.mjs#TA-25~28/38/39，范围读/写放行同 [../wiki/bdd.md#wx-07](../wiki/bdd.md#wx-07) | TA 21/21（2026-09-27） |
| @GW-S7 | team-apikey-scope-e2e.mjs#TA-32a/b/g/i ＋ external-app-e2e.mjs#EA-34a/b | TA 21/21、EA 74/74（2026-09-27） |
| @GW-S8 | team-apikey-scope-e2e.mjs#TA-33/34（换用户 token 门禁；刷新重验为代码路径） | TA 21/21（2026-09-27） |
| @GW-S9 | kg-external-e2e.mjs#KX-09a~i（kg_read/kg_write 分档、直连分档、补写刷新放行） | KX 67/67（2026-09-26） |
| @GW-S10 | team-apikey-scope-e2e.mjs#TA-36a/b（改范围/删除后直连下一请求即生效） | TA 21/21（2026-09-27） |
| 已下线团队 key 拒绝 | gateway-e2e.mjs（moai- 前缀网关 401）＋ team-apikey-scope-e2e.mjs#TA-32j（直连 401）＋ external-app-e2e.mjs#EA-36（外部接口 401） | GW 15/15、TA 21/21、EA 74/74（2026-09-27） |
| 前端 | ui/src/pages/teams/__tests__/TeamGateway.test.tsx（接入说明/可用模型/刷新/失败兜底）＋ TeamAccessApps.test.tsx | 4/4、3/3（2026-09-27） |

## 自检记录

- 2026-09-21~26 功能范围～六批历史记录见 rounds-log 与 git 历史（team_api_key 相关场景 TA-01~21/23/24/32e/f/35/37、GW-S1/S2/S4/S5 随团队接入 key 下线退役，编号不复用）。
- 2026-09-27 团队接入 key 下线：删除 `/api/team/{teamId}/gateway/keys` CRUD（Controller/4 Handler/Shared 模型）、`GatewayApiKeyAuthenticationHandler` 的 `moai-` 分支、`TeamApiKeyValidator`、`ExternalKeyCache` tk 路径、`ApiKeyGenerator.New`/`ApiKeySentinels`、换 token 的 `apiKey` 凭证与 `keyid` claim、`external_token` scope 位（8）；`team_api_key` 表删除（asserts/team_api_key_drop.sql，开发库已执行，存量 93 行随表清除）。网关认证仅接受 `moai-ac-`；`/api/external` key 直连与 MCP 门禁同口径收敛；`GatewayUsageService` 最近使用时间恒落 `access_app.last_used_time`。E2E：gateway-e2e 重写 **15/15**（应用接入 key 直连网关 + keys 端点 404）、team-apikey-scope **21/21**（TA-38a/b 三段式上传、TA-32j/TA-39/EA-36 已下线前缀 401）、external-app **74/74**；前端 syncapi（5023 独立实例）+ TeamGateway vitest 重写 4/4、typecheck/lint 0 error（KG Canvas WIP 既有除外）。
