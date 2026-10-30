# 团队网关模块验证映射（TDD）

> 关联：[SDD](./sdd.md) ｜ [BDD](./bdd.md) ｜ [TDD](./tdd.md) ｜ [SOP](./sop.md) ｜ 证据：[local-dev/team-apikey-scope-e2e.mjs](../../local-dev/team-apikey-scope-e2e.mjs)

## 映射表

| 场景 | 验证物 | 结果（日期） |
|---|---|---|
| @GW-S3 | team-apikey-scope-e2e.mjs#TA-32b（网关端点 404）＋ team-aichannel-e2e.mjs#TC-S5（chat/completions 与 models 端点下线断言） | TA 20/20、TC 35/35（2026-10-29） |
| @GW-S6 | local-dev/team-apikey-scope-e2e.mjs#TA-25~28/38/39，范围读/写放行同 [../wiki/bdd.md#wx-07](../wiki/bdd.md#wx-07)；TA-32a/i（model 代码 400） | TA 20/20（2026-10-29） |
| @GW-S7（已退役，断言改为下线） | team-apikey-scope-e2e.mjs#TA-32a/b/i ＋ external-app-e2e.mjs#EA-34a ＋ team-aichannel-e2e.mjs#TC-S5 | TA 20/20、EA 75/75、TC 35/35（2026-10-29） |
| @GW-S8 | team-apikey-scope-e2e.mjs#TA-33/34（换用户 token 门禁；刷新重验为代码路径） | TA 20/20（2026-10-29） |
| @GW-S9 | kg-external-e2e.mjs#KX-09a~i（kg_read/kg_write 分档、直连分档、补写刷新放行） | KX 101/101（2026-10-27） |
| @GW-S10 | team-apikey-scope-e2e.mjs#TA-36a/b（改范围/删除后直连下一请求即生效） | TA 20/20（2026-10-29） |
| 已下线团队 key 拒绝 | team-apikey-scope-e2e.mjs#TA-32j（直连 401）＋ external-app-e2e.mjs#EA-36（外部接口 401） | TA 20/20、EA 75/75（2026-10-29） |
| 团队可用模型（原网关 models 迁移） | team-aichannel-e2e.mjs#TC-S5（isTeamChannel 标记）＋ ai-channel-auth-e2e.mjs#AC-S4~S6（授权联动）＋ app-e2e.mjs#AP-20a（应用配置取值范围） | TC 35/35、AC 23/23、AP 210/210（2026-10-29） |

## 自检记录

- 2026-09-21~27 历史记录见 rounds-log 与 git 历史（team_api_key 相关场景 TA-01~21/23/24/32e/f/35/37、GW-S1/S2/S4/S5 随团队接入 key 下线退役，编号不复用；原 gateway-e2e.mjs（GW 15）随模型网关下线删除）。
- 2026-09-27 团队接入 key 下线：删除 `/api/team/{teamId}/gateway/keys` CRUD（Controller/4 Handler/Shared 模型）、`GatewayApiKeyAuthenticationHandler` 的 `moai-` 分支、`TeamApiKeyValidator`、`ExternalKeyCache` tk 路径、`ApiKeyGenerator.New`/`ApiKeySentinels`、换 token 的 `apiKey` 凭证与 `keyid` claim、`external_token` scope 位（8）；`team_api_key` 表删除（asserts/team_api_key_drop.sql，开发库已执行，存量 93 行随表清除）。
- 2026-10-29 团队模型网关下线：删除 `src/gateway/` 三项目与 `tests/MoAI.Gateway.Core.Tests`、sln 四项登记、宿主引用（MainModule/Program/MoAI.csproj）、`MoAI.App.Core` 对 Gateway.Shared 的引用与两个 Handler 的缓存失效钩子；`/api/aigateway/{teamId}/v1/*` 四端点、`TeamGatewayController`、`GatewayApiKeyAuthenticationHandler`、三入站/五上游协议、`GatewayUsageService`、`ExternalKeyCache`、`TeamApiKeyScopes.Model`（位 1 保留不用）与 `model` 代码全部移除；团队可用模型查询迁至 `GET /api/team/{teamId}/ai/model/available`（aichannel 模块，TC/AC/AP 三脚本改用）。前端删 `TeamGateway.tsx` 页面/菜单/测试与 `api/gateway.ts` 封装，`AppConfigSection`/`NodeForm` 改用 `aichannel.getTeamAvailableModels`，接入 key 表单去掉 model 勾选，i18n 删页面键（`gateway.scope.*` 保留）。验证：dotnet build 0 error；**TA 20/20、TC 35/35、EA 75/75、AC 23/23、AP 210/210**；前端 typecheck/lint 0 error、vitest 641/641（645→641 为 TeamGateway 4 用例随页面删除）。
