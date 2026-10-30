# 团队网关模块设计（SDD）

> 关联：[BDD](./bdd.md) ｜ [TDD](./tdd.md) ｜ [SOP](./sop.md) ｜ **[外部接口与范围体系细节总账](./external-api-scope-system.md)（改此模块前必读：scope 位表/端点分档/新增资源组清单/踩坑）** ｜ 上游：[../team/sdd.md](../team/sdd.md)、[../app/sdd.md](../app/sdd.md)（外部 token）｜ 证据：[local-dev/team-apikey-scope-e2e.mjs](../../local-dev/team-apikey-scope-e2e.mjs)

- 日期：2026-09-21 初版；2026-09-26 六批增补功能范围/直连/缓存；2026-09-27 团队接入 key（`team_api_key`，`moai-` 前缀）整体下线删除；**2026-10-29：团队模型网关整体下线删除**——`src/gateway/` 三项目（Shared/Core/Api）与单测工程全部移除，`/api/aigateway/{teamId}/v1/*` 四端点、`TeamGatewayController`、`model` scope 位、网关用量记账、`ExternalKeyCache` 缓存一并移除；模型仅供建模应用（对话/工作流/知识库等）内部调用，不再对外提供直连模型网关，**后续由独立子系统承接模型网关**。团队可用模型查询迁至 aichannel 模块 `GET /api/team/{teamId}/ai/model/available`（应用配置/流程节点模型选择器使用）
- 状态：外部接口范围体系（scope/换 token/key 直连/中间件分档）持续服务；模型网关面已下线

## 1. 目标

应用接入 key（`access_app`，`moai-ac-` 前缀）支持**功能范围**勾选：第三方持 key 只能调用勾选范围内、面向团队资源的外部开放接口，未勾选一律 403。团队外部接口按**资源组**划分，接入按组授权：

| 资源组 | 覆盖外部接口面 | scope 代码（位） | 应用接入 key |
|---|---|---|---|
| 知识库·读 | `/external/wiki/*` 读面（列表/详情/文档/正文/召回） | `wiki_read` (2) | ✅ |
| 知识库·写 | `/external/wiki/*` 写面（上传/提取/切割/向量化/删除） | `wiki_write` (4) | ✅ |
| 应用对话 | 换取用户 token + `/external/agent/*`、`/external/session/*` | `app_chat` (32) | ✅ |
| 知识库 MCP | `/external/wiki/{wikiId}/mcp` | `wiki_mcp` (16) | ✅ |
| 知识图谱·读 | `/external/knowledge-graph/*` 读面（列表/模式/节点/边查询） | `kg_read` (64) | ✅ |
| 知识图谱·写 | `/external/knowledge-graph/*` 写面（节点/边/实体类型/关系类型增删改） | `kg_write` (128) | ✅ |
| 知识图谱 MCP | `/external/knowledge-graph/{kgId}/mcp` | `kg_mcp` (256) | ✅ |
| 应用 ACP | `/external/app/{appId}/acp`（agent-to-agent 对话） | `app_acp` (512) | ✅ |
| 应用 A2A | `/external/app/{appId}/a2a`（Google Agent2Agent） | `app_a2a` (1024) | ✅ |

> ~~模型网关 `/api/aigateway/{teamId}/v1/*`（`model` 位 1）~~ 已随 2026-10-29 下线移除；位值 1 保留不用，存量 `access_app.scopes` 中的该位为无效残留，创建/更新接入勾选 `model` 一律 400。
> 团队接入 key 专属的 `external_token` (8) 位随下线一并移除（枚举与代码表已删）。
> 端点逐条清单、中间件分档规则、**新增资源组操作清单**与踩坑，见 [外部接口与范围体系细节总账](./external-api-scope-system.md)。

## 2. 组件

- `MoAI.Database.Shared/Enums/TeamApiKeyScopes.cs`：位标记枚举 + `TeamApiKeyScopeCodes`（代码↔标记互转，token claim 逗号串编解码；命名保留历史口径，现仅服务 access_app 体系；位 1 为已下线的 model 保留）。
- `MoAI.AIChannel`（团队可用模型查询，原网关 models 端点职责迁入）：`QueryTeamAvailableModelsCommand`（Shared）→ Handler（Core，聚合口径 = 团队自有渠道 ∪ 公开 ∪ 已授权，同名去重团队自有优先）→ `TeamAIModelController` `GET /api/team/{teamId}/ai/model/available`（Api，团队成员可访问）；消费方为应用配置与流程节点表单的模型选择器。
- `MoAI.App.Api/ExternalAuthenticationMiddleware` + `MoAI.App.Core/ExternalAccessKeyService`：`/api/external` 范围门禁与 key 直连（见 [../app/sdd.md](../app/sdd.md) 外部接入节）。

## 3. 数据

- `access_app.scopes int not null default 2038`：应用接入允许范围 = wiki_read/wiki_write/app_chat/wiki_mcp/kg_read/kg_write/kg_mcp/app_acp/app_a2a（`2=wiki_read 4=wiki_write 16=wiki_mcp 32=app_chat 64=kg_read 128=kg_write 256=kg_mcp 512=app_acp 1024=app_a2a`；位或；位 1（model）已下线保留不用）。DDL：[asserts/access_app_scopes.sql](../../asserts/access_app_scopes.sql)、[asserts/access_app_app_chat.sql](../../asserts/access_app_app_chat.sql)、[asserts/access_app_kg_scopes.sql](../../asserts/access_app_kg_scopes.sql)、[asserts/access_app_kg_mcp_scopes.sql](../../asserts/access_app_kg_mcp_scopes.sql)、[asserts/app_acp.sql](../../asserts/app_acp.sql)、[asserts/app_a2a.sql](../../asserts/app_a2a.sql)。
- `team_api_key` 表已删除（[asserts/team_api_key_drop.sql](../../asserts/team_api_key_drop.sql)，存量库执行删表；新库 EnsureCreated 不再建）。
- `access_app.last_used_time timestamptz null`：历史由网关记账刷新，网关下线后仅存量值。
- scope 代码对内枚举、对外小写下划线串（API 请求/响应、token `scope` claim 均用代码串）。

## 4. 关键决策

- **接入即 key**：外部资源走「接入 key → `/external/token` 换 token」或 key 直连两条路径，token 的 `scope` claim = 接入勾选中知识库维度（`ExternalDimensions` 掩码裁剪），`model/app_chat` 是接入层概念不进 token（model 已下线，仅 app_chat 仍为接入层概念）。
- **D-GW2 范围来源**：应用接入 key 签发的 token 范围取 `access_app.scopes` 勾选（未传 scopes 默认 `AccessAppDefault`=外部资源全量+对话；可配空=纯对话接入）；无 scope claim 的在途旧 token 解析侧仍按全量知识库范围处理；匿名 token 恒为全量。
- **D-GW4 刷新重验**：refresh token 刷新时按接入当前勾选范围与存在性重签——范围收紧或接入删除在下次刷新即生效（access token 本身在有效期内不回收）。
- **D-GW7 应用对话门禁**：用户 token 的用途是外部应用会话/对话，换取用户 token 要求接入勾选 `app_chat`（403），用户 token 刷新时按来源接入当前勾选重验——撤销对话范围在下次刷新即生效；直连 key 以「直连会话身份」外部用户承载会话归属。
- **D-GW9 中间件前置拦截**：`ExternalAuthenticationMiddleware` 在身份解析后按「路径+方法」静态分档判定所需范围——`/external/wiki` 与 `/external/knowledge-graph` 的 GET 与 `*/list` 为读档（`wiki_read`/`kg_read`），其余为写档（`wiki_write`/`kg_write`），MCP 路径另行要求 `wiki_mcp`/`kg_mcp` 不叠加读写档；未勾选在中间件层直接 403，控制器内保留同口径校验兜底。判定成本为常数次字符串比较，无 IO。
- **D-GW6 key 直连外部接口**：`/api/external` 的认证中间件接受 key 直连（`moai-ac-`，Bearer 或 x-api-key），上下文范围取接入勾选的知识库维度——见 [../app/sdd.md](../app/sdd.md) 外部接入节。直连每请求直查数据库（存在即有效），改范围/删除下一请求即生效（TA-36 验证）。
- **团队接入 key 下线（2026-09-27）**：`/api/team/{teamId}/gateway/keys` CRUD、`GatewayApiKeyAuthenticationHandler` 的 `moai-` 分支、`TeamApiKeyValidator`、`ExternalKeyCache` tk 路径、换 token/刷新的 `apiKey` 凭证与 `keyid` claim、`external_token` scope 位全部移除；存量团队 key 随删表立即失效，第三方改用应用接入 key（范围模型为其超集）。
- **团队模型网关下线（2026-10-29）**：`src/gateway/`（Shared/Core/Api 三项目 + `MoAI.Gateway.Core.Tests`）整体删除——`/api/aigateway/{teamId}/v1/chat/completions|responses|messages|models` 四端点、`GatewayApiKeyAuthenticationHandler`、`GatewayChatCore`/三入站协议（OpenAI Chat/Responses、Anthropic Messages）/五上游协议、`GatewayUsageService` 用量记账（`AiModelUseType.OpenApi` 位保留仅供存量数据）、`TeamGatewayController`+团队「模型网关」页、`ExternalKeyCache`（读路径仅网关使用）与 App 管理端失效钩子、`TeamApiKeyScopes.Model` 位与 `model` 代码、`AiModelUseType` 网关记账。应用侧不受影响：应用对话/工作流/知识库等经 AIChannel 服务内部调用模型；团队可用模型聚合查询迁至 `/api/team/{teamId}/ai/model/available`（口径与原网关 models 一致，去重后团队自有优先）。模型网关能力后续由**独立子系统**承接。

## 5. 已知问题

- access token 签发后范围在有效期内不回收（Debug 7 天 / Release 2 小时）；需要立即止损走「删除接入」+ 等待刷新失败或 token 自然过期。
- `/external/wiki` 尚无语义搜索外部端点，「知识库读」现覆盖 列表/详情/文档列表/正文/向量化查询；搜索端点上线后应同挂 `wiki_read` 校验。
- 下线前签发、携带 `keyid` claim 的存量 token：access 在有效期内仍可用（claims 不再解析 keyid），refresh 时按匿名/接入语义重验，来源 key 已随表删除。
- 存量 `access_app.scopes` 含 model 位（1）的行：该位为无效残留，不产生任何授权效果；下次改范围整体覆盖后自然清除。
