# 团队资源外部接口与凭证体系（深度细节）

> 关联：[SDD](./sdd.md)（设计决策 D-GW1~D-GW9，scope 体系已退役）｜ [BDD](./bdd.md) ｜ [TDD](./tdd.md) ｜ [SOP](./sop.md) ｜ 上游：[../app/sdd.md](../app/sdd.md)（外部 token 体系与 teamres_key 设计）、[../knowledgegraph/bdd.md](../knowledgegraph/bdd.md)（@KX-*）、[../wiki/bdd.md](../wiki/bdd.md)（@WX-*）｜ 证据：[local-dev/teamres-key-e2e.mjs](../../local-dev/teamres-key-e2e.mjs)（TR-*）
>
> 本文是「团队资源外部接口」的唯一细节总账：凭证体系、资源 key 包围圈、中间件门禁规则、兼容口径、**新增资源组的操作清单**与踩坑。改这块代码前先通读本文。
> 团队接入 key（`team_api_key`，`moai-` 前缀）已于 2026-09-27 下线删除；团队模型网关（`/api/aigateway/*`）已于 2026-10-29 整体下线删除。
> **scope 范围体系已于 2026-11-04 整体退役**（`access_app.scopes` 列 DROP、`TeamApiKeyScopes` 枚举删除）：moai-ac- 接入 key 不再勾选范围、**全量访问** `/api/teamres`；细粒度授权由**团队资源 key（`teamres_key`，`moai-tr-`）的包围圈**承接。原 §2 scope 位表/掩码与 §3 读/写分档口径随之退役，TA-* 场景编号永久不复用（TR-* 接任）。

## 1. 凭证体系总览（moai-ac- 全量 + moai-tr- 包围圈双轨）

| 凭证 | 表 / 前缀 | 签发者 | 可用性语义 |
|---|---|---|---|
| 应用接入 key | `access_app`（统一表，Level=team），`moai-ac-` + 随机 | 团队管理员（`/api/access-app`） | **存在即有效**（软删除自动过滤），**团队级全量访问 /api/teamres**（wiki 读写、KG 读写、agent 对话、ACP/A2A、wiki/KG MCP），无任何范围勾选；appid 明文可回显；配套密钥 PBKDF2 落库（原文仅创建/重置返回一次），已设密钥则兑换 token 必须携带 secret |
| 团队资源 key | `teamres_key`，`moai-tr-` + 32 位随机（前 12 位展示前缀） | **资源对外功能开启时自动生成/复用**（应用开 ACP/A2A `PUT /api/app/{id}/feature`、知识库/知识图谱开 MCP `PUT {id}/mcp-config`——查库无则写、有则复用；未开启功能手动 `POST /api/team-resource-key` 400）；管理面 `/api/team-resource-key`（get-or-create/重置；**key 无启停状态且不可删除**，团队 Admin+） | **包围圈凭证**：仅能访问绑定资源实例的端点（§2b），无 secret、**不可换 token**（`POST /api/teamres/token` 仅认 moai-ac-，携带 moai-tr- 400/401）；无启停状态（status 列已删），重置下一请求旧 key 即 401；**一资源一把**（app 行 resource_id=应用 Guid、wiki 行=wikiId、kg 行=kgId，`varchar(64)`），get-or-create 幂等；key 明文回显仅团队 Admin+（`ITeamService.GetMyRoleAsync` 门禁） |
| 外部 token（JWT） | 换取签发，不落库 | `POST /api/teamres/token` | access 2h（Debug 7d）+ refresh 7d；**不再携带 scope claim**（`ClaimScope` 已删），授权语义= 团队归属 + 外部用户身份；刷新按来源接入/应用**当前**状态重验。**应用 token** 仅认 moai-ac- key（无用户身份）；**匿名用户 token** 只传 appId（外部应用一律开放）；`accessAppKey + externalUserId` 换用户 token 已退役（400） |
| 访问用户 token（JWT） | 换取签发，不落库 | `POST /api/subsys/system/app-tokens`（系统级 `moai-sa-` 接入：`AppType=app` 或 `AppType=subsystem`） | access 2h + refresh 7d；`typ=normal`、sub=真实站内用户 id、claim `appid` 绑定单个内部应用；首次未绑定身份时自动创建用户并绑定 OAuth；会话落 `UserType.Normal` 与站内账号合并；刷新校验用户未禁用 + 应用发布 + 访问点开启；团队级 `moai-ac-` 不可换发 |
| 匿名临时身份 | 无 key | `POST /api/teamres/token` 只传 appId（外部应用一律开放，`is_auth` 已退役） | 刷新要求应用仍可用（发布、未禁用） |
| 系统级接入 appid（参照） | 同一张 `access_app`（Level=system，AppType 分型 subsystem/app），`moai-sa-` + Base64Url(Guid+HMAC) 结构化自校验 appid | root（`/api/subsystem/*`，合并管理面） | 与团队级同表分域：不能访问 /api/teamres、只能访问 /api/subsys/**（见 [../subsystem/sdd.md](../subsystem/sdd.md)），全量访问协作面资源 |

四条认证路径（对 `/api/teamres/*`）：
1. **换 token**：`POST /api/teamres/token` → 应用 token / 匿名用户 token；内部应用另经系统协作面 `POST /api/subsys/system/app-tokens` → 访问用户 token（四类见 [../app/sdd.md](../app/sdd.md) §2.2）。
2. **moai-ac- key 直连**：Bearer / `x-api-key` 直接带 key，`ExternalAccessKeyService.ResolveContextAsync` 按前缀构建直连上下文（`IsKeyDirect=true`，`Scopes` 字段已删）；直连发起会话走 `EnsurePrincipalUserAsync` 的固定直连身份（`__key_direct__`）承载归属。
3. **moai-tr- 资源 key 直连**：同上解析，上下文带 `IsResourceKey/ResourceKeyType/ResourceKeyId`，中间件前置**包围圈**判定（§2b）；直连身份标识按 key 行 Guid 派生（`__key_direct_{行id}__`，重置 key 值行 id 不变、身份延续），`external_user.access_app_id=null`。
4. **访问用户 token**：系统协作面按第三方登录绑定签发（`typ=normal`），Bearer 进入后仅能访问绑定内部应用（token `appid` = 路由 appId）且访问点须开启；会话归属真实站内用户。

## 2. scope 位表（已整体退役，2026-11-04 去范围化）

> **⚠️ 本节已退役**：`TeamApiKeyScopes` 枚举（wiki_read 2 / wiki_write 4 / wiki_mcp 16 / app_chat 32 / kg_read 64 / kg_write 128 / kg_mcp 256 / app_acp 512 / app_a2a 1024 位表）与全部掩码常量（`ExternalDimensions`/`AccessAppAllowed`/`AccessAppDefault`）已删除，`access_app.scopes` 列 DROP（[asserts/team-resource-key.sql](../../asserts/team-resource-key.sql) + `DatabasPostgresModule` 幂等 DDL）。创建/更新接入不再接收 scopes（请求携带——含非法代码——被忽略返回 200），列表项不再回显 scopes。token 不再写 scope claim，旧 token 的 scope claim 不再解析（`ExternalTokenContext.Scopes/DefaultScopes` 字段已删）。历史证据脚本 `team-apikey-scope-e2e.mjs`（TA 系列）删除，编号永久不复用。原「位 1=model / external_token=8 / 系统资源位表」各次退役说明仍见 git 历史与 [../subsystem/sdd.md](../subsystem/sdd.md)。

### 2b. 团队资源 key 包围圈（`IsResourceKeyAllowedAsync`，中间件前置拦截，越圈 403 insufficient_scope）与资源功能开关（403 feature_disabled）

**资源功能开关**（一资源一把 key 的生成时机与端点总闸；**key 自身无启停状态**——`teamres_key` 无 status 列、`PUT /api/team-resource-key/{id}` 启停端点已删除，启停只由功能入口开关控制，关闭即 403 `feature_disabled`、重开复用原 key，吊销手段=删除 key 或关闭开关）：

| 开关 | 字段 | 管理接口 | 未开启行为 |
|---|---|---|---|
| 应用对外协议接入（ACP 与 A2A 同开同关） | `app.is_acp_a2a_enabled` | `GET/PUT /api/app/{id}/feature`（body `{isAcpA2aEnabled}`，团队 Admin+；前端应用工作台单一「ACP/A2A」分区） | ACP 与 A2A 端点对**一切凭证**（moai-ac- / 资源 key / 用户 token）403 `feature_disabled`；开启时自动生成/复用该应用一把 key（响应与 GET 均回显，ACP/A2A 共用） |
| 知识库 MCP | `wiki.is_mcp_enabled` | `GET/PUT /api/wiki/{id}/mcp-config`（团队 Admin+） | 该库 MCP 端点 403 `feature_disabled`；**其资源 key 对该库任何端点（含 REST）403 feature_disabled**（key 的有效性绑定开关） |
| 知识图谱 MCP | `knowledge_graph.is_mcp_enabled`（仅托管图，接入图 400） | `GET/PUT /api/knowledge-graph/{id}/mcp-config` | 同知识库口径 |

**包围圈**（`teamres_key.resource_id` 存资源实例 id 字符串）：

| ResourceKeyType | 放行端点 |
|---|---|
| `app`（resourceId=应用 Guid） | 仅 `/api/teamres/app/{绑定appId}/acp` 与 `/api/teamres/app/{绑定appId}/a2a[.json]`（路由 appId 须等于绑定应用，打其它应用 403）；端点另受 `is_acp_a2a_enabled` 单开关门禁（403 feature_disabled）；wiki/agent 等其余端点一律 403 |
| `wiki`（resourceId=wikiId） | `/api/teamres/wiki/{绑定wikiId}/**`（含 `/mcp`）+ 团队级 `POST /api/teamres/wiki/list`（放行但结果经 `OnlyWikiId` 过滤只见绑定库）；其它库 403、越 KG/agent 端点 403；绑定库 MCP 关闭时一律 403 `feature_disabled` |
| `knowledgeGraph`（resourceId=kgId） | `/api/teamres/knowledge-graph/{绑定kgId}/**`（含 `/mcp`）+ 团队级 `POST /api/teamres/knowledge-graph/list`（`OnlyGraphId` 过滤）；同构 wiki |

- 包围圈在中间件按**路由资源 id 精确等值**判定（`TryGetRouteId`，id ≠ 绑定 id 即 403），团队级 list 端点精确路径匹配放行、由 handler 过滤结果（`QueryExternalWikisCommandHandler` / `QueryExternalGraphsCommandHandler`）。
- MCP 端点（`/api/teamres/wiki/{wikiId}/mcp`、`/api/teamres/knowledge-graph/{kgId}/mcp`）：接受 moai-ac-（团队隔离照旧 404）与**该资源自己的 moai-tr- key**；原 `wiki_mcp`/`kg_mcp` scope 概念删除。wiki/KG 控制器与 MCP tools 的 `RequireCaller` 已删 `requireWrite` 读写分档参数（签名简化），caller 模型新增 `OnlyWikiId/OnlyGraphId`。
- moai-ac- key 无包围圈（全量）；匿名 token 仅限外部应用（团队归属 + 发布/未禁用）；访问用户 token 仅限绑定内部应用且访问点开启。

## 3. 资源组 → 端点清单（授权 = 团队归属，无读/写分档）

原「GET 与 `*/list` = 读档，其余 = 写档」分档口径随 scope 退役删除（中间件 `GetRequiredResourceScope` 与控制器 `RequireCaller(requireWrite)` 兜底均已移除）；moai-ac- 对下列全部端点按**团队归属**放行。端点清单本身保留备查：

- 知识库 `/api/teamres/wiki/*`：`POST /list`、`GET /{wikiId}`、文档列表/正文/embedding/下载/召回（`POST /{wikiId}/recall/list`）等读面（7）+ embedding-config/上传/提取/切割/向量化/元数据生成/删除/重命名等写面（10）+ MCP `/api/teamres/wiki/{wikiId}/mcp`（三只读工具，见 wiki sdd D35）。
- 知识图谱 `/api/teamres/knowledge-graph/*`：列表/schema/节点/边查询读面（9）+ 节点/边/实体类型/关系类型增删改与批量导入写面（17）+ MCP `/api/teamres/knowledge-graph/{kgId}/mcp`（四只读工具，工具域由 `KnowledgeGraphMcpToolGate` 过滤）。
- 应用对话面：`POST /api/teamres/token`（匿名用户 token）、`/api/teamres/agent/{appId}/session*`、`/api/teamres/session/{sessionId}/messages` 等——外部应用接受匿名 token/key 直连（外部身份），内部应用仅接受访问用户 token（真实站内用户，绑定单应用 + 访问点开启）；应用 token 无用户身份不能建会话。原 `app_chat` 资格门禁已随 scope 退役删除。
- 应用 ACP：`POST /api/teamres/app/{appId}/acp`（Agent Client Protocol JSON-RPC 2.0；外部用户语义 + appId 归属团队 + 已发布未禁用 + **应用已开启对外协议接入**（未开启 403 feature_disabled），不要求 `IsExternal`；GET 405）。
- 应用 A2A：`POST /api/teamres/app/{appId}/a2a` 与 Agent Card `GET /a2a/agent.json`（Google Agent2Agent，门禁与 ACP 同口径，要求开启 A2A 功能）。
- 其他：`/api/teamres/app/list`（任意外部 token）、`/api/teamres/app/{appId}/access-point`（匿名）。

## 4. 中间件管道与门禁顺序（`ExternalAuthenticationMiddleware`）

```
/api/teamres/* → 匿名白名单？（token、token/refresh、app/*/access-point）
  → 凭证解析：moai-ac- / moai-tr- 前缀 → key 直连（ExternalAccessKeyService.ResolveContextAsync，每请求直查 DB）
             （moai-tr- 须 Status=Enabled，否则 null → 401）
             否则 Bearer → 外部 JWT（ExternalJwtBearerAuthenticationHandler → Items[TokenContextItemKey]）
  → ①资源 key 包围圈：IsResourceKeyPathAllowed（§2b，前置拦截，越圈 403 insufficient_scope；moai-ac-/token 不经过此步）
  → ②appId 门禁：路由含 appId 时校验应用可用（ACP/A2A 路径跳过走各自专属门禁）；
     外部应用：凭证团队归属 + 发布/未禁用（匿名开放，is_auth 已退役）；
     内部应用：仅访问用户 token（typ=normal 且 UserId>0），token appid=路由 appId，
     且访问点配置启用（关闭 403 access_point_disabled，非访问用户 token 404 不泄露）
  → ③wiki/KG MCP 门禁：IsWikiMcpPath/IsKgMcpPath 主体须接入 key/应用 token/资源 key（外部用户 token 拒绝）
     + 路由 id 归属凭证团队（404 不泄露）；非 POST 按协议 405
  → ④ACP/A2A 门禁：key 直连先 EnsurePrincipalUserAsync 解析直连会话身份 → 外部用户语义（ExternalId>0）
     + appId 归属团队（404）+ 已发布未禁用（403）
  → ⑤会话面直连身份：IsConversationPath 且 IsKeyDirect → EnsurePrincipalUserAsync（moai-tr- 到不了会话面，已被①拦截）
  → Controller：无 scope 兜底校验（RequireCaller 仅组上下文；wiki/KG caller 带 OnlyWikiId/OnlyGraphId）
```

## 5. key 直连的即时性（每请求直查数据库）

- 现口径：`/api/teamres` 的两类 key 直连每请求经 `ExternalAccessKeyService.ResolveContextAsync` **直查数据库**——moai-ac- 「存在即有效」（软删除由查询自动过滤），moai-tr- 另校验 `Status=Enabled`；删除/停用/重置/改授权天然**下一请求即生效**（TR-S24~S28、ACP-S16~S17、A2A-S13 验证），无 TTL 等待。
- 原 `ExternalKeyCache`（TTL 10 分钟）已随 2026-10-29 模型网关下线删除。
- 性能账：key 直连每请求 = 1 次数据库查询 + 常数次字符串比较（包围圈前缀/路由 id 判定）。

## 6. 兼容口径（改默认值/回填前必读）

- **scopes 退役迁移**：`access_app.scopes` 列已 DROP（[asserts/team-resource-key.sql](../../asserts/team-resource-key.sql)，与 `DatabasPostgresModule` 幂等 DDL 等价，存量库重启即生效或手工执行）；`teamres_key` 建表同文件。创建/更新接入传入 `scopes`（含非法代码）被忽略返回 200（TC-S5 验证）。
- **「不传 scopes 默认全量」口径已消亡**：不再存在 DB DEFAULT / handler 默认 / E2E 断言三方一致性坑；旧坑（KG 上线时 38 vs 230 不一致 KX 全红）成历史。
- **旧格式 token**（曾携带 `scope` claim）：claim 不再解析，上下文无 scope 字段，行为=全量（团队门禁不变）；无需换签。
- **已下线团队 key 的存量 token**：携带 `keyid` claim 的在途 token claims 不再解析 keyid，access 有效期内仍可用；refresh 按匿名/接入语义重验（来源 key 已随表删除，通常 401）。
- **资源 key 纯直连**：无 secret 列、不可换 token；`POST /api/teamres/token` 仅认 moai-ac- 前缀（携带 moai-tr- key 400/401，TR-S27）。伪造 moai-tr-/moai-ac- 与旧 `moai-` 前缀 key 均 401（TR-S29）。
- **管理端 secret 语义不变**：应用接入已设密钥则兑换 token 必须携带 secret 并校验（存量无密钥接入 key-only 兼容）。

## 7. 新增外部资源组操作清单（无 scope 版，按序执行）

1. 若需资源 key 包围圈：`TeamResourceType` 枚举加成员（`JsonPropertyName` camelCase wire 值）+ 中间件 `IsResourceKeyAllowedAsync` 加分支（路由 id 等值 + 团队级 list 放行）+ `CreateTeamResourceKeyCommandHandler` 加归属与**功能开启校验**（未开启 400）+ caller 模型加 `OnlyXxxId` 并在团队级 list Handler 过滤。
2. 控制器/MCP tools：`RequireCaller()` 简化签名组 caller（无 requireWrite 参数）；新端点无需任何范围校验。
3. 中间件：资源型端点按 §4 管道复用既有门禁；团队归属校验在中间件或 Handler（跨团队 404 不泄露存在性）；**功能开关**：资源实体加 `is_xxx_enabled` 列（幂等 DDL 补列）+ 专属 GET/PUT 配置接口（PUT 开启时调 `TeamResourceKeyEnsurer.EnsureAsync` 自动生成/复用 key）+ 中间件端点门禁校验开关（未开启 403 `feature_disabled`，资源 key 在 `EnsureResourceFeatureAsync` 一并校验）。
4. 存量兼容：新表/新列走 `asserts/<feature>.sql`（幂等 DDL）+ `DatabasPostgresModule` 同步（`EnsureCreated` 无迁移体系）。
5. UI：对应资源的功能页挂开关（调 PUT 配置接口）+ `ResourceKeyCard`（`ui/src/pages/teams/apps/ResourceKeyCard.tsx` 公共组件，props `featureEnabled/canManage/keyInfo/onChanged`——key 由父页配置接口取回，无手动生成按钮）+ i18n `teamresKey.*`（zh-CN/en-US 同步）。
6. E2E：`teamres-key-e2e.mjs` 加用例（未开功能建 key 400/开功能自动生成并 GET 回显/get-or-create 幂等/包围圈放行与越圈 403/关功能 feature_disabled/list 过滤/重置停用删除生命周期/伪造 401）+ 模块脚本回归（读放行/跨团队隔离/直连）。
7. 文档：模块 bdd 场景（编号顺延、永不复用）+ tdd 映射 + sdd 同步 + 本总账端点清单加行 + rounds-log。

## 8. 踩坑清单（全部实踩）

- **手动重建的 Command 不走 MVC 校验**（SharpGrip 只校验绑定参数）：`TeamResourceKeyController` 的 reset/update/delete 由 Controller 手动重建命令、`TeamResourceKeyId` 从路由回填——字段合法性须在命令 `Validate`（非空校验）与 Handler（行存在 404）把关。
- **枚举 wire 值 = `JsonPropertyName` 成员名**：`TeamResourceType.KnowledgeGraph` 对外是 `"knowledgeGraph"`（非成员名或数字），E2E/前端请求体必须按 camelCase json 名传；漏配注解时会以数字或 PascalCase 落请求，`IsInEnum` 校验 400 且难以肉眼定位。
- **资源 key 明文回显是 Admin+ 敏感面**：list/create/reset 均经 `ITeamService.GetMyRoleAsync` 门禁（Member 403、非成员 404），且 get-or-create 对 wiki/kg 校验**资源归属团队**（越团队 404 不泄露存在性）——新增消费入口不得绕过这层门禁直接暴露 key。
- **i18n 扁平键**：模块 i18n 键（如 `teamresKey.*`）是组内带点扁平键，脚本加键**禁止** `setdefault` 同名嵌套父对象（空对象会让 i18next 深查找不回退，t() 返回裸 key）；原 `gateway.scope.*` 键组已随 scope 退役删除。
- **缓存失效钩子已成历史**：删除/停用/重置后下一请求即生效由「直连每请求直查 DB」天然保证（TR-S25/S26、ACP-S17a/b、A2A-S13b/c 验证），无需任何失效钩子。
- **Scalar 2.17 单文档绑定**：`AddDocument` 是覆盖语义，双文档用双页面 `/scalar`（v1）+ `/scalar/external`（external），原始 JSON 为 `/openapi/v1.json`、`/openapi/teamres.json`；均为 Development 专属，部署后仅 `/openapi/teamres.json` 可用（v1 与 Scalar 不注册，其余 openapi/scalar 路径明确 404）。
- **多前缀判定顺序**：`moai-ac-`/`moai-tr-` 均以 `moai-` 开头，key 直连提取按**完整前缀**（`AccessAppKeyPrefix`/`ResourceKeyPrefix`）逐一 `StartsWith` 判定（`ExtractKeyCredential`），旧 `moai-` 前缀 key 不命中任何分支按外部 JWT 解析失败 401；新增凭证前缀时接入 key 判定必须在前（历史坑：`moai-ac-` 曾淹没于 `moai-`）。
- **统一表后按 Level 过滤**：团队侧查询（ExternalTokenCommandHandler 等）一律 `Level == Team`，系统侧一律 `Level == System`。已设密钥的接入兑换 token 必须携带 secret；存量行 secret 为空继续 key-only。
- **PBKDF2 哈希/盐 Base64 后 172 字符**：access_app.secret/secret_salt 列宽必须 ≥255（曾配 128 导致插入 500，已改 255 并同步迁移脚本）。
- **teamres_key 唯一性靠 (key, is_deleted) 部分唯一索引**：key 明文等值命中（无哈希），软删后同 key 值可被新行复用；get-or-create 按 (team, type, resource) 查活跃行，重置=同表行改 key 值（行 id 不变——直连身份 `__key_direct_{行id}__` 因此延续，勿改用「删旧建新」实现重置）。

## 9. 证据索引

| 面 | 脚本 | 现状 |
|---|---|---|
| 凭证体系总账户（去范围化 + 资源 key） | `local-dev/teamres-key-e2e.mjs`（@TR-S1~S31） | 31/31 |
| 知识库外部接口 | `local-dev/wiki-external-e2e.mjs`（WX-01~12；WX-09e 去范围化正向、WX-12 资源 key 包围圈） | 57/57 |
| 知识图谱外部接口 | `local-dev/kg-external-e2e.mjs`（KX-01~15；KX-15 资源 key 包围圈） | 110/110 |
| 知识库 MCP | `local-dev/wiki-mcp-e2e.mjs`（@WM-S1~S8；S1e 越库资源 key 403、S8 本库资源 key 放行） | 34/34 |
| 知识图谱 MCP | `local-dev/kg-mcp-e2e.mjs`（@KGM-S1~S8；S1e 越域资源 key 403、S8 本图资源 key 放行） | 36/36 |
| 应用 ACP | `local-dev/app-acp-e2e.mjs`（@ACP-S01~S19；S02a/b 包围圈、S16~S17 资源 key 停用吊销即时性、S19 列表无 scopes） | 25/25 |
| 应用 A2A | `local-dev/app-a2a-e2e.mjs`（@A2A-S01~S14；S02a/b 包围圈、S13a~c 资源 key 吊销即时性、S14 列表无 scopes） | 24/24 |
| 外部应用对话（token 体系） | `local-dev/external-app-e2e.mjs`（EA-*；EA-30a/b 去范围化） | 75/75 |
| 团队可用模型（原网关 models 迁移） | `local-dev/team-aichannel-e2e.mjs`（TC-S5 含 scopes 字段退役被忽略断言）、`ai-channel-auth-e2e.mjs`（AC-S4~S6）、`app-e2e.mjs`（AP-20a） | 38/38、23/23、227/227 |
| 前端资源 key UI | `ui/src/pages/teams/apps/__tests__/TeamAccessApps.test.tsx` | 6/6 |
| 已退役 | ~~`local-dev/team-apikey-scope-e2e.mjs`（TA 系列，scope 体系总账户）~~ | 已删除，编号不复用 |
