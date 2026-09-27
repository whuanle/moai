# 团队资源外部接口与范围体系（深度细节）

> 关联：[SDD](./sdd.md)（设计决策 D-GW1~D-GW9）｜ [BDD](./bdd.md) ｜ [TDD](./tdd.md) ｜ [SOP](./sop.md) ｜ 上游：[../app/sdd.md](../app/sdd.md)（外部 token 体系）、[../knowledgegraph/bdd.md](../knowledgegraph/bdd.md)（@KX-*）、[../wiki/bdd.md](../wiki/bdd.md)（@WX-*）
>
> 本文是「团队资源外部接口」的唯一细节总账：接入 key、scope 位、两条认证路径、中间件门禁规则、Redis 缓存、兼容口径、**新增资源组的操作清单**与踩坑。改这块代码前先通读本文。
> 团队接入 key（`team_api_key`，`moai-` 前缀）已于 2026-09-27 下线删除，本文均以应用接入 key 为唯一 key 凭证。

## 1. 凭证体系总览

| 凭证 | 表 / 前缀 | 签发者 | 可用性语义 |
|---|---|---|---|
| 应用接入 key | `access_app`，`moai-ac-` + 随机 | 团队管理员（`/api/access-app`） | **存在即有效**（软删除自动过滤），key 明文可回显 |
| 外部 token（JWT） | 换取签发，不落库 | `POST /api/external/token` | access 2h（Debug 7d）+ refresh 7d；刷新按来源接入**当前**勾选重验 |
| 匿名临时身份 | 无 key | `POST /api/external/token` 只传 appId（应用须 `is_auth=false`） | 刷新要求应用仍无需授权 |

两条认证路径（对 `/api/external/*` 等价）：
1. **换 token**：接入 key → 外部 token → Bearer JWT。token 的 `scope` claim = 来源勾选 ∩ 外部资源维度。
2. **key 直连**：Bearer / `x-api-key` 直接带 key 打资源接口，中间件经 `ExternalAccessKeyService.ResolveContextAsync` 构建直连上下文（带 `IsKeyDirect=true`）。直连发起会话走 `EnsurePrincipalUserAsync` 的固定直连身份承载会话归属。

## 2. scope 位表（`TeamApiKeyScopes`，`access_app.scopes` int 位或）

| 代码 | 位 | 资源组 | 应用接入 key | 进 token/直连上下文？ |
|---|---|---|---|---|
| `model` | 1 | 模型网关 `/api/aigateway/{teamId}/v1/*` | ✅（直连网关） | ❌ 接入层概念 |
| `wiki_read` | 2 | 知识库读 | ✅ | ✅ |
| `wiki_write` | 4 | 知识库写 | ✅ | ✅ |
| `wiki_mcp` | 16 | 知识库 MCP（见 wiki sdd D35） | ✅ | ✅ |
| `app_chat` | 32 | 应用对话（换用户 token 资格） | ✅ | ❌ 换 token/刷新时校验 |
| `kg_read` | 64 | 知识图谱读 | ✅ | ✅ |
| `kg_write` | 128 | 知识图谱写 | ✅ | ✅ |
| `kg_mcp` | 256 | 知识图谱 MCP（/api/external/knowledge-graph/{kgId}/mcp 四只读工具） | ✅ | ✅ |

> `external_token` (8) 随团队接入 key 下线一并移除（枚举与代码表已删）。

关键掩码（都在 `TeamApiKeyScopeCodes`，改动位表必须同步）：
- `ExternalDimensions`（外部资源维度 = 2|4|16|64|128|256）：token/直连上下文能携带的范围；**换 token 与直连的掩码都由它裁剪**。
- `AccessAppAllowed`（应用接入可勾选 = 1|2|4|16|32|64|128|256）：接入 key 全集。
- `AccessAppDefault`（接入未传 scopes 的默认 = ExternalDimensions|AppChat = 486）：「不传=存量全量」口径。
- `ExternalTokenContext.DefaultScopes`（无 scope claim 的旧 token 兼容 = ExternalDimensions 全量）。

## 3. 资源组 → 端点清单与分档规则

分档统一口径：**GET 与 `*/list` 结尾 = 读档，其余 = 写档**；同口径在中间件（前置拦截）与控制器（兜底）各实现一次。

### 知识库 `/api/external/wiki/*`（读 `wiki_read` / 写 `wiki_write`）
- 读（5）：`POST /list`、`GET /{wikiId}`、`POST /{wikiId}/documents/list`、`GET /{wikiId}/documents/{documentId}/content`、`GET /{wikiId}/documents/{documentId}/embedding`
- 写（9）：`PUT /{wikiId}/embedding-config`、`POST /{wikiId}/documents/preupload|complete`、`DELETE /{wikiId}/documents`、`PUT /{wikiId}/documents/{documentId}/rename`、`POST /{wikiId}/documents/{documentId}/extract|partition|ai-partition|embedding`
- MCP 端点（已上线）：`/external/wiki/{wikiId}/mcp`（要求 `wiki_mcp`，不叠加读写档；三只读工具=知识库列表/文件搜索/向量召回，见 wiki sdd D35）

### 知识图谱 `/api/external/knowledge-graph/*`（读 `kg_read` / 写 `kg_write`）
- 读（9）：`POST /list`、`GET /{kgId}/schema`、`POST /{kgId}/nodes/list`、`GET /{kgId}/nodes/{nodeId}`、`GET /{kgId}/nodes/by-key/{key}`、`POST /{kgId}/nodes/keys/list`、`GET /{kgId}/nodes/{nodeId}/neighbors`、`POST /{kgId}/edges/list`、`GET /{kgId}/edges/{edgeId}`
- 写（17）：`POST/PUT/DELETE /{kgId}/nodes[/{nodeId}]`、`POST /{kgId}/nodes/batch`、`POST /{kgId}/nodes/batch-delete`、`POST/PUT/DELETE /{kgId}/edges[/{edgeId}]`、`POST /{kgId}/edges/batch`、`POST /{kgId}/edges/batch-delete`、`POST/PUT/DELETE /{kgId}/entity-types[/{typeId}]`、`POST/PUT/DELETE /{kgId}/relation-types[/{typeId}]`、`POST /{kgId}/import`（批量导入 + validateOnly 预检，面向 CSV/JSON 解析后的结构化写入与按 key 同步，语义见 [../knowledgegraph/sdd.md](../knowledgegraph/sdd.md) §5.1）
- MCP 端点：`/external/knowledge-graph/{kgId}/mcp`（要求 `kg_mcp`，不叠加读写档；四只读工具=list_knowledge_graphs/get_knowledge_graph_schema/search_knowledge_graph_nodes/search_knowledge_graph_recall；与知识库 MCP 共享 McpServerOptions，工具域由 KnowledgeGraphMcpToolGate 按请求路径过滤，未知工具保持 SDK 原生 -32602）

### 应用对话面（`app_chat` 在换 token 时校验，端点层无 scope 门禁）
`POST /external/token`（用户 token）、`/external/agent/{appId}/session*`、`/external/session/{sessionId}/messages` 等——仅外部用户 token；`app_chat` 撤销后用户 token 刷新即 403。

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

## 5. Redis 缓存（`ExternalKeyCache`，Gateway.Shared）

- 键：`externalkey:ac:{sha256(key原文)}`；TTL 10 分钟；**只缓存可用实例快照**（id/团队/scopes）。
- **失效钩子（变更立即生效，无 TTL 等待）**：`UpdateAccessAppCommandHandler`/`DeleteAccessAppCommandHandler`（按 `ApiKeyGenerator.Hash(entity.Key)`）。
- **不进缓存**：创建者账号状态等可用性语义——应用接入 key「存在即有效」，软删除由查询自动过滤。
- 性能账：key 直连每请求 = 1 次数据库查询 + 常数次字符串比较（中间件分档）。

## 6. 兼容口径（改默认值/回填前必读）

- **存量回填历史**（开发库 192.168.50.199/moai_v2 已执行，脚本在 `asserts/`）：`access_app` 6=读写 → `|=32` 补对话 → `|=192` 补知识图谱 → `|=256` 补 KG MCP；`team_api_key` 已随下线删表（asserts/team_api_key_drop.sql）。
- **「未传 scopes」默认**：应用接入 = `AccessAppDefault`(486)。两处必须一致：DB 列 DEFAULT、handler 默认（`CreateAccessAppCommandHandler`），E2E 断言同口径（KX 曾因 38/230 不一致全红）。
- **旧格式 token**（无 `scope` claim）：解析侧按 `DefaultScopes` 全量处理，行为不变。
- **已下线团队 key 的存量 token**：携带 `keyid` claim 的在途 token claims 不再解析 keyid，access 有效期内仍可用；refresh 按匿名/接入语义重验（来源 key 已随表删除，通常 401）。
- `model`/`app_chat` 是接入层概念，**永不写入 token/直连上下文**；新资源维度默认加进 `ExternalDimensions`。

## 7. 新增外部资源组操作清单（按序执行）

1. `TeamApiKeyScopes` 加位（2 的幂依次递增）+ `Codes` 字典加代码；`ExternalDimensions` 扩位；加 `AccessAppAllowed`；适用「不传默认全量」则加 `AccessAppDefault`。
2. EF 位表注释同步：`AccessAppConfiguration` 的 `Scopes.HasComment`。
3. 存量兼容迁移：新建 `asserts/<feature>.sql`（`alter default` + `update ... |= 位`，接入表通常回填）→ **psycopg2 直连 192.168.50.199/moai_v2 执行**（`DatabasPostgresModule` 是 EnsureCreated，无迁移体系）。
4. 控制器：`RequireCaller(requireWrite)` 分档（读 `Resources{Read}`、写 `{Write}`），`Caller.AccessAppId` 保持可空。
5. 中间件：`GetRequiredResourceScope` 加路径分支（沿用 GET/`*/list`=读 口径；特例路径先于分档短路）。
6. UI：`TeamAccessApps.tsx` 分组编辑器加组（三档 Radio 复用 `levelOf/setLevel`）+ i18n `gateway.scope.*`（zh/en，`gateway` 组内扁平键）。
7. 换 token/直连掩码自动生效，无需额外改码；若属「key 层资格」类（如 app_chat）则去 `ExternalTokenCommandHandler`/`RefreshExternalTokenCommandHandler` 加门禁。
8. E2E：范围脚本加用例（读放行/写 403/跨资源组隔离/直连分档/改范围刷新重验/缓存立即性）。
9. 文档：gateway sdd 分组表加行 + bdd 场景（`@GW-S*` 顺延）+ tdd 映射 + 模块 bdd（对应资源组）+ rounds-log。

## 8. 踩坑清单（全部实踩）

- **手动重建的 Command 不走 MVC 校验**（SharpGrip 只校验绑定参数）：范围代码合法性必须在 handler 里把关（`BusinessException 400`）。
- **i18n 扁平键**：`common.json` 的 `gateway.scope.*` 是 `gateway` 组内的带点扁平键，脚本加键**禁止** `setdefault` 同名嵌套父对象（空对象会让 i18next 深查找不回退，t() 返回裸 key，页面标签全坏且无报错）。
- **默认口径多处一致性**（DB DEFAULT / handler 默认 / E2E 断言）——KG 上线时 38 vs 230 不一致导致 KX 全红 403。
- **缓存失效钩子挂在管理端 handler**：删除/改范围后下一请求即生效（TA-36 验证），别依赖 TTL 到期。
- **Scalar 2.17 单文档绑定**：`AddDocument` 是覆盖语义，双文档用双页面 `/scalar`（v1）+ `/scalar/external`（external），原始 JSON 为 `/openapi/v1.json`、`/openapi/external.json`。
- **双前缀判定顺序**（历史）：`moai-ac-` 以 `moai-` 开头，前缀分流时接入 key 判定必须在前；团队 key 下线后仅剩 `moai-ac-` 单前缀，中间件 key 直连提取同样只认 `moai-ac-`。

## 9. 证据索引

| 面 | 脚本 | 现状 |
|---|---|---|
| 范围体系总账户 | `local-dev/team-apikey-scope-e2e.mjs`（TA-25~28/32/33/34/36/38/39） | 21/21 |
| 知识库外部接口 | `local-dev/wiki-external-e2e.mjs`（WX-01~06） | 30/30 |
| 知识图谱外部接口 | `local-dev/kg-external-e2e.mjs`（KX-01~14） | 101/101 |
| 知识图谱 MCP | `local-dev/kg-mcp-e2e.mjs`（KGM-S1~S7） | 35/35 |
| 外部应用对话（token 体系） | `local-dev/external-app-e2e.mjs`（EA-*） | 74/74 |
| 网关（model 范围 + keys 端点下线） | `local-dev/gateway-e2e.mjs` | 15/15 |
| 前端范围 UI | `ui/src/pages/teams/__tests__/TeamGateway.test.tsx`、`TeamAccessApps.test.tsx` | 4/4、3/3 |
