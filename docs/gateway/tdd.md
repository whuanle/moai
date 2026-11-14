# 团队网关模块验证映射（TDD）

> 关联：[SDD](./sdd.md) ｜ [BDD](./bdd.md) ｜ [TDD](./tdd.md) ｜ [SOP](./sop.md) ｜ 证据：[local-dev/teamres-key-e2e.mjs](../../local-dev/teamres-key-e2e.mjs)

## 映射表

| 场景 | 验证物 | 结果（日期） |
|---|---|---|
| @GW-S3 | team-aichannel-e2e.mjs#TC-S5（chat/completions 与 models 端点下线断言 + scopes 字段退役被忽略创建 200） | TC 38/38（2026-11-04） |
| @GW-S6 | teamres-key-e2e.mjs#TR-S1~S9（不传/传 scopes 均 200、列表无 scopes、wiki 读写+KG list+app token 兑换+x-api-key 直连；key+externalUserId 换用户 token 退役断言见 TR-S9）；正向断言另见 wiki-external-e2e.mjs#WX-09e | TR 39/39、WX 57/57（2026-11-13） |
| @GW-S7（已退役，断言改为下线） | team-aichannel-e2e.mjs#TC-S5 ＋ external-app-e2e.mjs#EA-34a | TC 38/38、EA 75/75（2026-11-04） |
| @GW-S8（已退役，app_chat 概念删除；用户 token 亦随外部应用授权退役） | teamres-key-e2e.mjs#TR-S9（key+externalUserId 返回 400 并指向访问点） | TR 39/39（2026-11-13） |
| @GW-S9（已退役，kg 分档删除） | kg-external-e2e.mjs#KX-09a~h（全量 token/直连读写均放行、跨资源组 wiki/list 放行） | KX 110/110（2026-11-04） |
| @GW-S10 | teamres-key-e2e.mjs#TR-S24~S28（重置/停用/删除即时生效）＋ ACP-S17a/b、A2A-S13b/c（资源 key 吊销即时性） | TR 31/31、ACP 25/25、A2A 24/24（2026-11-04） |
| @GW-S11 | teamres-key-e2e.mjs#TR-S16~S23（wiki/app key 包围圈：本库 REST+MCP 放行、越库/越圈 403、团队级 list 过滤）；模块侧 WX-12、KX-15、WM-S1e/S8、KGM-S1e/S8、ACP-S02a/b、A2A-S02a/b | TR 31/31、WX 57/57、KX 110/110、WM 34/34、KGM 36/36（2026-11-04） |
| @GW-S12 | teamres-key-e2e.mjs#TR-S10~S14/S24~S28/S27（get-or-create 幂等/归属 404/重置行 id 不变/停用启用/删除/不可换 token）；前端 ResourceKeyCard 交互见 [@TR-S32](../app/bdd.md#tr-s32)~[@TR-S35](../app/bdd.md#tr-s35) | TR 31/31（2026-11-04） |
| 已下线团队 key 拒绝 | teamres-key-e2e.mjs#TR-S29（旧 moai- 前缀 401）＋ external-app-e2e.mjs#EA-36 | TR 31/31、EA 75/75（2026-11-04） |
| 团队可用模型（原网关 models 迁移） | team-aichannel-e2e.mjs#TC-S5（isTeamChannel 标记）＋ ai-channel-auth-e2e.mjs#AC-S4~S6（授权联动）＋ app-e2e.mjs#AP-20a（应用配置取值范围） | TC 38/38、AC 23/23、AP 227/227（2026-11-04） |

## 自检记录

- 2026-09-21~27 历史记录见 rounds-log 与 git 历史（team_api_key 相关场景 TA-01~21/23/24/32e/f/35/37、GW-S1/S2/S4/S5 随团队接入 key 下线退役，编号不复用；原 gateway-e2e.mjs（GW 15）随模型网关下线删除）。
- 2026-10-29 团队模型网关下线：删除 `src/gateway/` 三项目与单测、`/api/aigateway/*` 四端点、`ExternalKeyCache`、`TeamApiKeyScopes.Model` 位；团队可用模型查询迁至 `GET /api/team/{teamId}/ai/model/available`。历史细节见 rounds-log 轮 224。
- 2026-11-04 scope 体系退役 + 统一团队资源 key：删除 `TeamApiKeyScopes` 枚举与 `TeamApiKeyScopeCodes` 全部掩码、`access_app.scopes` 列（asserts/team-resource-key.sql DROP + 幂等 DDL）、token scope claim（ClaimScope/DefaultScopes）、中间件分档判定与 `RequireCaller(requireWrite)` 分档、`app_chat` 兑换门禁；新建 `teamres_key` 表 + `/api/team-resource-key` 管理端（get-or-create/重置/启停/删除）+ `IsResourceKeyPathAllowed` 包围圈 + `OnlyWikiId/OnlyGraphId` list 过滤 + 前端 `ResourceKeyCard` 三处挂载；删除 `local-dev/team-apikey-scope-e2e.mjs`（TA 编号永久退役），新建 `local-dev/teamres-key-e2e.mjs`（@TR-S1~S31）。验证：dotnet build 0 error；**TR 31/31、WX 57/57、KX 110/110、WM 34/34、KGM 36/36、ACP 25/25、A2A 24/24、EA 75/75、TC 38/38**；dotnet test Subsystem 53/53 + Client 8/8；ui typecheck/lint 0 error、vitest 748/748。
