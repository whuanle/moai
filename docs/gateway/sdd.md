# 团队网关模块设计（SDD）

> 关联：[BDD](./bdd.md) ｜ [TDD](./tdd.md) ｜ [SOP](./sop.md) ｜ **[外部接口与凭证体系细节总账](./external-api-scope-system.md)（改此模块前必读：凭证双轨/资源 key 包围圈/中间件管道/新增资源组清单/踩坑）** ｜ 上游：[../team/sdd.md](../team/sdd.md)、[../app/sdd.md](../app/sdd.md)（外部 token 与 teamres_key）｜ 证据：[local-dev/teamres-key-e2e.mjs](../../local-dev/teamres-key-e2e.mjs)

- 日期：2026-09-21 初版；2026-09-27 团队接入 key（`team_api_key`，`moai-` 前缀）整体下线删除；2026-10-29 团队模型网关整体下线删除（`src/gateway/` 三项目移除，模型仅供建模应用内部调用，后续由独立子系统承接）；**2026-11-04：scope 范围体系整体退役 + 统一团队资源 key（teamres_key）**——`TeamApiKeyScopes` 枚举与全部掩码常量删除、`access_app.scopes` 列 DROP，moai-ac- 接入 key 全量访问 `/api/teamres`，细粒度授权改由 `teamres_key`（`moai-tr-`）包围圈承接
- 状态：外部接口凭证体系（moai-ac- 全量 + moai-tr- 包围圈 + 换 token）持续服务；模型网关面与 scope 体系均已下线

## 1. 目标

团队外部资源（知识库、知识图谱、团队应用 ACP/A2A）的第三方接入采用**凭证双轨**：

| 凭证 | 授权粒度 | 适用方 |
|---|---|---|
| 应用接入 key（`moai-ac-`，`access_app`） | 团队级**全量** `/api/teamres`（无任何范围勾选） | 业务系统后端持 key 换 token / 直连调任意团队外部接口 |
| 团队资源 key（`moai-tr-`，`teamres_key`） | **包围圈**：仅绑定资源实例端点（**一资源一把**：应用行=应用 Guid（其 ACP/A2A 共用）、知识库行=wikiId、知识图谱行=kgId；不同实例 key 互不相同） | 用户复制「端点地址 + key」即可接入单个资源（MCP 客户端、ACP agent 等），无 secret 不可换 token；**key 唯一生成时机=资源开启对外功能**（应用 `is_acp_enabled/is_a2a_enabled`、库/图 `is_mcp_enabled`，开启时自动生成/复用，未开启端点 403 feature_disabled） |

> ~~scope 范围体系（wiki_read/wiki_write/wiki_mcp/app_chat/kg_read/kg_write/kg_mcp/app_acp/app_a2a 位表与读/写分档）~~ 已随 2026-11-04 去范围化退役：`access_app.scopes` 列 DROP、枚举/掩码删除、创建接入传 scopes 被忽略 200、token 不再写 scope claim；TA-* 证据脚本删除编号不复用。位表/掩码/分档口径的历史细节见 git 历史与 [外部接口与凭证体系细节总账](./external-api-scope-system.md) §2 退役注记。

## 2. 组件

- `MoAI.Database.Shared/Entities/TeamResourceKeyEntity.cs` + `Enums/TeamResourceKeyEnums.cs`：资源 key 实体与 `TeamResourceType`（app=1/wiki=2/knowledgeGraph=3，JsonPropertyName camelCase wire 值）/`TeamResourceKeyStatus`（enabled=0/disabled=1）枚举；`DbSet` 于 `DatabaseContext.TeamResourceKeys`。
- `MoAI.App.Shared/Services/TeamResourceKeyGenerator.cs`：key 生成器（`moai-tr-` + 32 位随机字母数字，前 12 位为展示前缀）；`ExternalAuthDefaults.ResourceKeyPrefix`。
- `MoAI.App.Core/Services/ExternalAccessKeyService.cs`：`ResolveContextAsync` 按 moai-ac-/moai-tr- 双前缀分支解析直连上下文（资源 key 须 Enabled）；`EnsurePrincipalUserAsync` 直连身份（资源 key 按 `__key_direct_{行id}__` 派生、`access_app_id=null`）。
- `MoAI.App.Api/ExternalAuthenticationMiddleware.cs`：`/api/teamres` 认证 + 资源 key 包围圈 `IsResourceKeyAllowedAsync`（前置拦截：路由资源 id 等值 + 绑定资源功能开启校验，越圈/未开启 403）+ appId/MCP/ACP/A2A 各专属门禁（ACP/A2A/MCP 端点校验应用/库/图功能开关，未开启 403 `feature_disabled`）。
- `MoAI.App.Api/Controllers/TeamResourceKeyController.cs`（`/api/team-resource-key`，团队 Admin+ 门禁）+ `MoAI.App.Core/Handlers/TeamResourceKeyHandlers.cs`（get-or-create/列表/重置/启停/软删）。
- `MoAI.AIChannel`（团队可用模型查询，原网关 models 端点职责迁入）：`GET /api/team/{teamId}/ai/model/available`。

## 3. 数据

- `teamres_key`：`id uuid / team_id int / resource_type int / resource_id bigint（app 行=0）/ key varchar(255) 明文（唯一部分索引 (key,is_deleted)）/ key_prefix varchar(16)（前 12 位）/ status int / 审计四件 + is_deleted`。DDL：[asserts/team-resource-key.sql](../../asserts/team-resource-key.sql)（与 `DatabasPostgresModule` 幂等 DDL 等价，存量库执行即得）。
- `access_app.scopes` 列已 DROP（同上文件 `ALTER TABLE ... DROP COLUMN IF EXISTS scopes`）；token 不再写 scope claim。
- `team_api_key` 表已删除（asserts/team_api_key_drop.sql）；`access_app.last_used_time` 为网关下线后的存量值。

## 4. 关键决策

- **D-GW2（修订）凭证双轨授权模型**：应用接入 key 不再携带任何范围概念——存在即团队级全量（换 token / 直连同口径）；细粒度授权下沉到资源 key，以「路径包围圈」而非「位掩码」表达，授权单位从资源组收缩到资源实例。
- **D-GW9（修订）包围圈前置拦截取代分档判定**：`ExternalAuthenticationMiddleware` 在凭证解析后按 `IsResourceKeyPathAllowed` 判定 moai-tr- 可达端点——路由资源 id 与绑定 id 精确等值、团队级 list 端点放行交 handler 过滤（`OnlyWikiId/OnlyGraphId`）；越圈统一 403 insufficient_scope。原「GET 与 `*/list`=读档」分档口径与控制器 `RequireCaller(requireWrite)` 兜底一并删除（签名简化）。
- **D-GW4（修订）刷新重验**：refresh token 刷新时按接入**当前存在性与 secret** 重签（范围概念已删）——接入删除在下次刷新即生效；access token 有效期内不回收。
- **D-GW7（退役）应用对话门禁**：`app_chat` 位随 scope 体系删除，兑换/刷新用户 token 不再要求任何资格位；会话面门禁保留「需授权（is_auth）应用仅外部用户 token」与 appId 归属团队校验。
- **D-GW6 key 直连外部接口**：`/api/teamres` 认证中间件接受 Bearer/x-api-key 携带 moai-ac-/moai-tr- 直连；每请求直查数据库，删除/停用/重置下一请求即生效（TR-S24~S28、ACP-S17a/b 验证）。资源 key 为纯直连凭证：不可换 token（`POST /api/teamres/token` 仅认 moai-ac-）。
- **资源 key 生命周期（2026-11-04）**：get-or-create 幂等（同 (团队,类型,资源) 原样返回 created=false）；重置=同表行重生成 key（行 id 不变，直连身份延续）；停用 401、启用恢复、删除（软删）401；wiki/kg 生成时校验资源归属团队否则 404。
- **团队接入 key 下线（2026-09-27）/ 模型网关下线（2026-10-29）**：历史决策原文见 git 历史——前者删除 `team_api_key` 表与 `moai-` 单前缀认证，后者删除 `src/gateway/` 三项目与 `/api/aigateway/*` 四端点，团队可用模型查询迁至 aichannel 模块。

## 5. 已知问题

- access token 签发后在有效期内不回收（Debug 7 天 / Release 2 小时）；需要立即止损走「删除接入」+ 等待刷新失败或 token 自然过期；资源 key 无此问题（直连每请求校验，停用即 401）。
- `/api/teamres/wiki` 尚无语义搜索外部端点；资源 key 的 list 过滤仅覆盖团队级列表端点，其余团队维度聚合端点新增时须同步挂 `OnlyXxxId` 过滤。
- 资源 key 明文落库（无哈希）+ 管理端明文回显：便利性优先的取舍（用户复制即用），门禁依赖团队 Admin+ 与传输层安全；若需审计增强可加最后使用时间列（现无）。
- 下线前签发、携带 `keyid`/`scope` claim 的存量 token：access 在有效期内仍可用（claims 不再解析），refresh 时按匿名/接入语义重验。
