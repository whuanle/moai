# 团队资源外部接口与范围体系（深度细节）

> 关联：[SDD](./sdd.md)（设计决策 D-GW1~D-GW9）｜ [BDD](./bdd.md) ｜ [TDD](./tdd.md) ｜ [SOP](./sop.md) ｜ 上游：[../app/sdd.md](../app/sdd.md)（外部 token 体系）、[../knowledgegraph/bdd.md](../knowledgegraph/bdd.md)（@KX-*）、[../wiki/bdd.md](../wiki/bdd.md)（@WX-*）
>
> 本文是「团队资源外部接口」的唯一细节总账：接入 key、scope 位、两条认证路径、中间件门禁规则、兼容口径、**新增资源组的操作清单**与踩坑。改这块代码前先通读本文。
> 团队接入 key（`team_api_key`，`moai-` 前缀）已于 2026-09-27 下线删除，本文均以应用接入 key 为唯一 key 凭证。
> **团队模型网关（`/api/aigateway/*`、`model` scope 位）已于 2026-10-29 整体下线删除**：模型仅供建模应用内部调用，后续由独立子系统承接；`ExternalKeyCache` 缓存随之移除（读路径仅网关使用）。

## 1. 凭证体系总览

| 凭证 | 表 / 前缀 | 签发者 | 可用性语义 |
|---|---|---|---|
| 应用接入 appid | `access_app`（统一表，Level=team），`moai-ac-` + 随机 | 团队管理员（`/api/access-app`） | **存在即有效**（软删除自动过滤），appid 明文可回显；配套密钥 PBKDF2 落库（原文仅创建/重置返回一次） |
| 外部 token（JWT） | 换取签发，不落库 | `POST /api/external/token` | access 2h（Debug 7d）+ refresh 7d；刷新按来源接入**当前**勾选重验；**接入已设密钥则兑换必须携带 secret 并校验**（存量无密钥接入 key-only 兼容） |
| 匿名临时身份 | 无 key | `POST /api/external/token` 只传 appId（应用须 `is_auth=false`） | 刷新要求应用仍无需授权 |
| 系统级接入 appid（参照） | 同一张 `access_app`（Level=system），`moai-sa-` + 随机 | root（`/api/subsystem/system-app`） | 与团队级同表分域：不能访问 /api/external、只能访问 /api/subsys/system/*（见 [../subsystem/sdd.md](../subsystem/sdd.md)）；scope 位空间隔离（§2b） |

两条认证路径（对 `/api/external/*` 等价）：
1. **换 token**：接入 key → 外部 token → Bearer JWT。token 的 `scope` claim = 来源勾选 ∩ 外部资源维度。
2. **key 直连**：Bearer / `x-api-key` 直接带 key 打资源接口，中间件经 `ExternalAccessKeyService.ResolveContextAsync` 构建直连上下文（带 `IsKeyDirect=true`）。直连发起会话走 `EnsurePrincipalUserAsync` 的固定直连身份承载会话归属。

## 2. scope 位表（`TeamApiKeyScopes`，`access_app.scopes` int 位或）

| 代码 | 位 | 资源组 | 应用接入 key | 进 token/直连上下文？ |
|---|---|---|---|---|
| `wiki_read` | 2 | 知识库读 | ✅ | ✅ |
| `wiki_write` | 4 | 知识库写 | ✅ | ✅ |
| `wiki_mcp` | 16 | 知识库 MCP（见 wiki sdd D35） | ✅ | ✅ |
| `app_chat` | 32 | 应用对话（换用户 token 资格） | ✅ | ❌ 换 token/刷新时校验 |
| `kg_read` | 64 | 知识图谱读 | ✅ | ✅ |
| `kg_write` | 128 | 知识图谱写 | ✅ | ✅ |
| `kg_mcp` | 256 | 知识图谱 MCP（/api/external/knowledge-graph/{kgId}/mcp 四只读工具） | ✅ | ✅ |
| `app_acp` | 512 | 应用 ACP（/api/external/app/{appId}/acp，agent-to-agent 对话） | ✅ | ✅ |
| `app_a2a` | 1024 | 应用 A2A（/api/external/app/{appId}/a2a，Google Agent2Agent JSON-RPC） | ✅ | ✅ |

> 位 1（`model`，模型网关 `/api/aigateway/{teamId}/v1/*`）已随 2026-10-29 网关下线保留不用：枚举成员与代码表项已删，勾选一律 400，存量行该位为无效残留。
> `external_token` (8) 随团队接入 key 下线一并移除（枚举与代码表已删）。

### 2b. 系统资源 scope 位表（`SystemApiKeyScopes`，同列高位）

同一 `access_app.scopes` 列，系统级接入（Level=system）使用 1<<16 起的高位，与团队低位天然隔离（Level 过滤 + 位空间双保险）：

| 代码 | 位 | 资源组 |
|---|---|---|
| `org_read` | 1<<16 (65536) | 组织角色树/成员关系读 |
| `org_write` | 1<<17 (131072) | 组织角色写与用户角色分配 |
| `user_read` | 1<<18 (262144) | 平台用户读 |
| `user_write` | 1<<19 (524288) | 用户 upsert/禁用 |
| `team_read` | 1<<20 (1048576) | 团队列表读 |
| `team_write` | 1<<21 (2097152) | 团队创建/更新/成员管理 |

系统级接口挂 `/api/subsys/system/*`（协作面 /openapi/subsys.json），鉴权双轨（appid 直连或 system_app_access JWT），细节见 [../subsystem/sdd.md](../subsystem/sdd.md)。存量迁移见 [asserts/access_app_unify.sql](../../asserts/access_app_unify.sql)。

关键掩码（都在 `TeamApiKeyScopeCodes`，改动位表必须同步）：
- `ExternalDimensions`（外部资源维度 = 2|4|16|64|128|256|512|1024）：token/直连上下文能携带的范围；**换 token 与直连的掩码都由它裁剪**。
- `AccessAppAllowed`（应用接入可勾选 = 2|4|16|32|64|128|256|512|1024）：接入 key 全集。
- `AccessAppDefault`（接入未传 scopes 的默认 = ExternalDimensions|AppChat = 2038）：「不传=存量全量」口径。
- `ExternalTokenContext.DefaultScopes`（无 scope claim 的旧 token 兼容 = ExternalDimensions 全量）。

## 3. 资源组 → 端点清单与分档规则

分档统一口径：**GET 与 `*/list` 结尾 = 读档，其余 = 写档**；同口径在中间件（前置拦截）与控制器（兜底）各实现一次。

### 知识库 `/api/external/wiki/*`（读 `wiki_read` / 写 `wiki_write`）
- 读（7）：`POST /list`、`GET /{wikiId}`、`POST /{wikiId}/documents/list`、`GET /{wikiId}/documents/{documentId}/content`、`GET /{wikiId}/documents/{documentId}/embedding`、`GET /{wikiId}/documents/{documentId}/download`、`POST /{wikiId}/recall/list`（向量召回，复用 wiki_mcp 的召回命令；`/list` 后缀即读档口径，勿改名去掉后缀）
- 写（10）：`PUT /{wikiId}/embedding-config`、`POST /{wikiId}/documents/preupload|complete`、`DELETE /{wikiId}/documents`、`PUT /{wikiId}/documents/{documentId}/rename`、`POST /{wikiId}/documents/{documentId}/extract|partition|ai-partition|embedding`、`POST /{wikiId}/documents/{documentId}/chunks/metadata/generate`
- MCP 端点（已上线）：`/external/wiki/{wikiId}/mcp`（要求 `wiki_mcp`，不叠加读写档；三只读工具=知识库列表/文件搜索/向量召回，见 wiki sdd D35）

### 知识图谱 `/api/external/knowledge-graph/*`（读 `kg_read` / 写 `kg_write`）
- 读（9）：`POST /list`、`GET /{kgId}/schema`、`POST /{kgId}/nodes/list`、`GET /{kgId}/nodes/{nodeId}`、`GET /{kgId}/nodes/by-key/{key}`、`POST /{kgId}/nodes/keys/list`、`GET /{kgId}/nodes/{nodeId}/neighbors`、`POST /{kgId}/edges/list`、`GET /{kgId}/edges/{edgeId}`
- 写（17）：`POST/PUT/DELETE /{kgId}/nodes[/{nodeId}]`、`POST /{kgId}/nodes/batch`、`POST /{kgId}/nodes/batch-delete`、`POST/PUT/DELETE /{kgId}/edges[/{edgeId}]`、`POST /{kgId}/edges/batch`、`POST /{kgId}/edges/batch-delete`、`POST/PUT/DELETE /{kgId}/entity-types[/{typeId}]`、`POST/PUT/DELETE /{kgId}/relation-types[/{typeId}]`、`POST /{kgId}/import`（批量导入 + validateOnly 预检，面向 CSV/JSON 解析后的结构化写入与按 key 同步，语义见 [../knowledgegraph/sdd.md](../knowledgegraph/sdd.md) §5.1）
- MCP 端点：`/external/knowledge-graph/{kgId}/mcp`（要求 `kg_mcp`，不叠加读写档；四只读工具=list_knowledge_graphs/get_knowledge_graph_schema/search_knowledge_graph_nodes/search_knowledge_graph_recall；与知识库 MCP 共享 McpServerOptions，工具域由 KnowledgeGraphMcpToolGate 按请求路径过滤，未知工具保持 SDK 原生 -32602）

### 应用对话面（`app_chat` 在换 token 时校验，端点层无 scope 门禁）
`POST /external/token`（用户 token）、`/external/agent/{appId}/session*`、`/external/session/{sessionId}/messages` 等——仅外部用户 token；`app_chat` 撤销后用户 token 刷新即 403。

### 应用 ACP（`app_acp`，agent-to-agent）
`POST /external/app/{appId}/acp`：把团队应用（Agent 应用与流程应用，内部/外部均可）以 ACP 协议（Agent Client Protocol，JSON-RPC 2.0）暴露给外部 agent。门禁在中间件 ACP 专属分支：外部用户语义（用户 token 或 key 直连解析出的直连会话身份）+ `app_acp` + appId 归属团队（跨团队 404）+ 已发布未禁用（不要求 `IsExternal`，不叠加 `app_chat`）；GET 405。方法面：`initialize`/`session/new`/`session/load`/`session/prompt`（SSE 流式下发 `session/update` 通知，stopReason 收口）/`session/cancel`；会话即应用会话（`app_agent_session`，归属外部用户 id），执行管线与对话面同源（见 app sdd 增量设计）。

### 应用 A2A（`app_a2a`，Google Agent2Agent）
`POST /external/app/{appId}/a2a`：把团队应用（Agent 应用与流程应用）以 A2A 协议（JSON-RPC 2.0）暴露给外部 agent，门禁与 ACP 同口径（外部用户语义 + `app_a2a` + appId 归属团队 + 已发布）。方法面：`message/send`（同步一轮对话，返回 Task，artifacts 携带回复文本）、`message/stream`（SSE：Task working → artifact-update 文本增量 → status-update completed/failed/canceled final → 最终 Task）、`tasks/get`、`tasks/cancel`（进程内任务注册表，尽力而为）；`contextId` 承载 MoAI 会话 id（缺省自动建会话，携带即续聊，归属外部用户）。Agent Card 发现：`GET /external/app/{appId}/a2a/agent.json`（protocolVersion 0.3.0、streaming 能力、skills）。执行管线与对话面/ACP 同源。

### 其他
`/external/app/list`（任意外部 token）、`/external/app/{appId}/access-point`（匿名）。

## 4. 中间件管道与门禁顺序（`ExternalAuthenticationMiddleware`）

```
/api/external/* → 匿名白名单？（token、token/refresh、app/*/access-point）
  → 凭证解析：moai-ac- 前缀 → key 直连（ExternalAccessKeyService）
             否则 Bearer → 外部 JWT（ExternalJwtBearerAuthenticationHandler → Items[TokenContextItemKey]）
  → ①资源范围门禁：GetRequiredResourceScope(path, method)（§3 口径，未勾选 403 insufficient_scope）
  → ②appId 门禁：路由含 appId 时校验应用属 token 团队且可用（is_auth 应用仅外部用户 token）
  → ③wiki/KG MCP 门禁：IsWikiMcpPath/IsKgMcpPath 要求对应 scope + 路由 id 属团队
  → Controller：控制器内保留同口径 scope 兜底校验（RequireCaller(requireWrite)）
```

## 5. key 直连的即时性（原 Redis 缓存已移除）

- ~~`ExternalKeyCache`（`externalkey:ac:{sha256}`，TTL 10 分钟）~~：读路径仅模型网关认证使用，已随 2026-10-29 网关下线删除，`UpdateAccessAppCommandHandler`/`DeleteAccessAppCommandHandler` 的失效钩子一并移除。
- 现口径：`/api/external` 的 key 直连每请求经 `ExternalAccessKeyService.ResolveContextAsync` **直查数据库**（应用接入 key「存在即有效」，软删除由查询自动过滤），改范围/删除天然下一请求即生效（TA-36 验证），无 TTL 等待。
- 性能账：key 直连每请求 = 1 次数据库查询 + 常数次字符串比较（中间件分档）。

## 6. 兼容口径（改默认值/回填前必读）

- **存量回填历史**（开发库 192.168.50.199/moai_v2 已执行，脚本在 `asserts/`）：`access_app` 6=读写 → `|=32` 补对话 → `|=192` 补知识图谱 → `|=256` 补 KG MCP → `|=512` 补应用 ACP 且列 DEFAULT 对齐 1014（app_acp.sql）→ `|=1024` 补应用 A2A 且列 DEFAULT 对齐 2038（app_a2a.sql）；`team_api_key` 已随下线删表（asserts/team_api_key_drop.sql）。
- **「未传 scopes」默认**：应用接入 = `AccessAppDefault`(2038)。两处必须一致：DB 列 DEFAULT、handler 默认（`CreateAccessAppCommandHandler`），E2E 断言同口径（KX 曾因 38/230 不一致全红）。
- **旧格式 token**（无 `scope` claim）：解析侧按 `DefaultScopes` 全量处理，行为不变。
- **已下线团队 key 的存量 token**：携带 `keyid` claim 的在途 token claims 不再解析 keyid，access 有效期内仍可用；refresh 按匿名/接入语义重验（来源 key 已随表删除，通常 401）。
- **存量 model 位残留**：`access_app.scopes` 含位 1 的行不产生任何授权效果；创建/更新接入传 `model` 代码一律 400（TA-32a/i 验证）。
- `app_chat` 是接入层概念，**永不写入 token/直连上下文**；新资源维度默认加进 `ExternalDimensions`。

## 7. 新增外部资源组操作清单（按序执行）

1. `TeamApiKeyScopes` 加位（2 的幂依次递增）+ `Codes` 字典加代码；`ExternalDimensions` 扩位；加 `AccessAppAllowed`；适用「不传默认全量」则加 `AccessAppDefault`。
2. EF 位表注释同步：`AccessAppConfiguration` 的 `Scopes.HasComment`。
3. 存量兼容迁移：新建 `asserts/<feature>.sql`（`alter default` + `update ... |= 位`，接入表通常回填）→ **psycopg2 直连 192.168.50.199/moai_v2 执行**（`DatabasPostgresModule` 是 EnsureCreated，无迁移体系）。
4. 控制器：`RequireCaller(requireWrite)` 分档（读 `Resources{Read}`、写 `{Write}`），`Caller.AccessAppId` 保持可空。
5. 中间件：`GetRequiredResourceScope` 加路径分支（沿用 GET/`*/list`=读 口径；特例路径先于分档短路）。
6. UI：`TeamAccessApps.tsx` 分组编辑器加组（三档 Radio 复用 `levelOf/setLevel`）+ i18n `gateway.scope.*`（zh/en，`gateway` 组内扁平键；组名 `gateway` 为历史命名，模型网关已下线，仅 scope 标签仍在使用）。
7. 换 token/直连掩码自动生效，无需额外改码；若属「key 层资格」类（如 app_chat）则去 `ExternalTokenCommandHandler`/`RefreshExternalTokenCommandHandler` 加门禁。
8. E2E：范围脚本加用例（读放行/写 403/跨资源组隔离/直连分档/改范围刷新重验/缓存立即性）。
9. 文档：gateway sdd 分组表加行 + bdd 场景（`@GW-S*` 顺延）+ tdd 映射 + 模块 bdd（对应资源组）+ rounds-log。

## 8. 踩坑清单（全部实踩）

- **手动重建的 Command 不走 MVC 校验**（SharpGrip 只校验绑定参数）：范围代码合法性必须在 handler 里把关（`BusinessException 400`）。
- **i18n 扁平键**：`common.json` 的 `gateway.scope.*` 是 `gateway` 组内的带点扁平键，脚本加键**禁止** `setdefault` 同名嵌套父对象（空对象会让 i18next 深查找不回退，t() 返回裸 key，页面标签全坏且无报错）。
- **默认口径多处一致性**（DB DEFAULT / handler 默认 / E2E 断言）——KG 上线时 38 vs 230 不一致导致 KX 全红 403。
- **缓存失效钩子挂在管理端 handler**：删除/改范围后下一请求即生效（TA-36 验证）。原 `ExternalKeyCache` 钩子已随网关下线移除，现即时性由「直连每请求直查 DB」天然保证。
- **Scalar 2.17 单文档绑定**：`AddDocument` 是覆盖语义，双文档用双页面 `/scalar`（v1）+ `/scalar/external`（external），原始 JSON 为 `/openapi/v1.json`、`/openapi/external.json`；均为 Development 专属，部署后仅 `/openapi/external.json` 可用（v1 与 Scalar 不注册，其余 openapi/scalar 路径明确 404）。
- **双前缀判定顺序**（历史）：`moai-ac-` 以 `moai-` 开头，前缀分流时接入 key 判定必须在前；团队 key 下线后仅剩 `moai-ac-` 单前缀，中间件 key 直连提取同样只认 `moai-ac-`。
- **统一表后按 Level 过滤**：团队侧查询（ExternalTokenCommandHandler 等）一律 `Level == Team`，系统侧一律 `Level == System`——漏加 Level 过滤时系统行 TeamId=0 不会被团队查询命中，但显式过滤防 future 面新增。已设密钥的接入兑换 token 必须携带 secret（TA-40/41）；存量行 secret 为空继续 key-only。
- **PBKDF2 哈希/盐 Base64 后 172 字符**：access_app.secret/secret_salt 列宽必须 ≥255（曾配 128 导致插入 500，已改 255 并同步迁移脚本）。

## 9. 证据索引

| 面 | 脚本 | 现状 |
|---|---|---|
| 范围体系总账户 | `local-dev/team-apikey-scope-e2e.mjs`（TA-25~28/32/33/34/36/38~42；TA-40~42 密钥兑换语义） | 24/24 |
| 知识库外部接口 | `local-dev/wiki-external-e2e.mjs`（WX-01~10） | 52/52 |
| 知识图谱外部接口 | `local-dev/kg-external-e2e.mjs`（KX-01~14） | 101/101 |
| 知识图谱 MCP | `local-dev/kg-mcp-e2e.mjs`（KGM-S1~S7） | 35/35 |
| 应用 ACP | `local-dev/app-acp-e2e.mjs`（@ACP-S1~S19） | 21/21 |
| 外部应用对话（token 体系） | `local-dev/external-app-e2e.mjs`（EA-*；EA-34 网关下线断言） | 75/75 |
| 团队可用模型（原网关 models 迁移） | `local-dev/team-aichannel-e2e.mjs`（TC-S5）、`ai-channel-auth-e2e.mjs`（AC-S4~S6）、`app-e2e.mjs`（AP-20a） | 35/35、23/23、210/210 |
| 前端范围 UI | `ui/src/pages/teams/apps/__tests__/TeamAccessApps.test.tsx` | 3/3 |
