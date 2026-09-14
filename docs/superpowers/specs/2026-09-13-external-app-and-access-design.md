# 外部应用 · 应用接入 · 外部用户 Token 设计

- 日期：2026-09-13
- 状态：一期、二期已交付；**三期部分交付**（2026-09-14）：`external_user` 表、`/external/token`（应用/用户/匿名三类 token，access+refresh 双令牌）、`/external/token/refresh`、`/external/app/list` 与 `/api/external` 拦截器已实现并 E2E 28/28 全绿；**外部会话与对话端点（`/external/agent/*`、`/external/session/*`）为下阶段**。落地细节见 [app SDD §2.2](../../app/sdd.md) 与 [@EA-S*](../../app/bdd.md)。
- 关联：[应用 SDD](../../app/sdd.md) ｜ [团队 SDD](../../team/sdd.md) ｜ [CQRS 规范](../../cqrs-conventions.md) ｜ [前端规范](../../../ui/docs/frontend-conventions.md) ｜ [数据库脚手架](../../database-scaffold/sdd.md) ｜ [应用接口真源](../../api_interface.md)

## 背景与目标

平台现有「团队应用」只服务内部用户。本期引入**内部应用 / 外部应用**的区分，并补齐外部接入链路，使外部系统可以把平台上的外部应用挂到自己的网站上使用：

- **内部应用**：团队内使用；可设置 `is_public`，让平台内任意用户使用。
- **外部应用**：仅外部用户/匿名可使用；平台内部用户在应用中看不到。
  - `is_auth=false`：无需授权，外部网站或第三方系统可直接（换取临时身份后）使用。
  - `is_auth=true`：必须经「应用接入」申请外部用户临时 token 才能访问。

同时修复「用量来源资源 id」在不同资源类型（应用=uuid、知识库=int、用户=bigint）下无法统一落库的类型冲突。

## 范围

**包含：**
1. 外部应用：`app.is_external / is_auth / is_public` 语义落地、团队「外部应用」菜单、可见性过滤、平台公开应用列表。
2. 应用接入：团队下 `access_app`（授权 key + 可访问外部应用列表）的增删改查。
3. 外部用户访问：`external_user` 表、`/external/token` 换取临时 token、`/external/...` 外部对话端点、用量归属。
4. 用量表类型修复：`ai_model_usage_log.use_resource_id`、`ai_model_token_audit.use_resource_id` 统一为字符串；`ai_model_token_audit.user_id` 改 bigint。

**不包含（后续迭代）：**
- 外部用户注册/账号体系、外部用户界面（本期外部用户只通过 API/嵌入式前端使用）。
- 外部应用的独立计费/额度中心（沿用现有团队额度与用量统计）。
- 外部 token 的可视化吊销控制台（先靠禁用 `access_app` / `app` 实现）。
- MCP/OpenAPI 形态的外部应用。

## 术语（避免混淆）

| 名称 | 含义 |
|---|---|
| `app` | 应用。`is_external=false` 内部应用，`true` 外部应用；`is_auth` 仅外部应用有意义；`is_public` 仅内部应用有意义。 |
| `access_app` | **应用接入**。团队下创建的一把 key，授权它可访问哪些外部应用（`app_ids`）。**不含 `is_auth` 字段**（`is_auth` 属于 `app`）。 |
| `external_user` | **外部用户**。一次访问所对应的外部身份记录；`id` 承载会话归属与用量归属。 |

## 数据模型（DB-first）

schema 走手写 DDL + PostgresScaffold 逆向；`EnsureCreated` 不演进既有库。新增 DDL 放 `asserts/external_app.sql`，改库后重跑脚手架并 `dotnet build` 回归。

### 1. `app`（复用已加列）

已存在（未提交改动）：`is_external`、`is_auth`、`is_public`、`publish_status`、`publish_time`。

- `is_external` 唯一区分内外；`is_auth` 仅在 `is_external=true` 时有意义；`is_public` 仅在 `is_external=false` 时有意义。
- **废弃 `enable_foreign`**：`ALTER TABLE app DROP COLUMN enable_foreign;`，并移除实体/配置/命令/DTO/前端引用。

### 2. `access_app`（复用，保持既有结构）

保持用户已建结构，不改列：

| 列 | 类型 | 说明 |
|---|---|---|
| `id` | uuid | 主键 |
| `name` | varchar(20) | 接入名称 |
| `description` | varchar(255) | 描述 |
| `team_id` | int | 所属团队 |
| `key` | varchar(255) | 接入 key，**明文**存储；创建时生成并仅在创建响应返回一次 |
| `app_ids` | uuid[] | 允许访问的外部应用 id 列表 |
| 审计五件套 | | `create_user_id/create_time/update_user_id/update_time/is_deleted` |

- key 生成规则：`moai-ac-` + 32 位随机串（展示用前缀 `moai-ac-` + 8 位）。
- 约束：`app_ids` 中每个应用必须属于本团队且 `is_external=true`；否则 400。

### 3. `external_user`（新建，外部用户）

> 落地更名（2026-09-14）：原设计表名 `external` 过于泛化、难以辨识，落地时更名为 `external_user`；实体 `ExternalUserEntity`、DbSet `ExternalUsers` 同步命名。

| 列 | 类型 | 说明 |
|---|---|---|---|
| `id` | bigint identity | 主键；承载会话 `create_user_id` 与用量 `user_id` |
| `team_id` | int | 归属团队 |
| `app_id` | uuid null | 匿名访问（`is_auth=false`）时的来源应用 |
| `access_app_id` | uuid null | 应用接入访问（`is_auth=true`）时的来源接入 |
| `external_user_id` | varchar(128) | 外部身份标识；绑定值（如 ERP 用户号）或临时随机值 |
| `nickname` | varchar(100) | 可选显示名（来自请求） |
| 审计五件套 | | |

- partial 唯一索引 `(access_app_id, external_user_id) WHERE access_app_id IS NOT NULL AND is_deleted = 0`：同一接入下同一外部身份复用同一条记录，从而**继承聊天记录与消费记录**。
- 匿名/临时身份：`external_user_id` 每次生成随机值，形成独立记录，不继承。

### 4. 用量表类型修复

问题：应用 id=uuid、知识库 id=int、用户 id=bigint，现有列类型装不下，且 `ai_model_token_audit.user_id` 为 int（内部用户 id 本就可能溢出）。

- `ai_model_usage_log.use_resource_id`：`int` → `varchar(64)`。
- `ai_model_token_audit.use_resource_id`：`uuid` → `varchar(64)`，唯一索引 `(model_id, team_id, user_id, use_type, use_resource_id)` 随列类型重建。
- `ai_model_token_audit.user_id`：`int` → `bigint`。
- 编码约定：uuid 用 `D` 字符串，数值用十进制字符串；`use_type` 已能区分资源种类，不新增 `resource_type`。
- 代码影响：`IAiModelUsageCounter.IncrementAsync` 的 `useResourceId` 由 `Guid` 改 `string`；`AiModelUsageCounterDimension` / Redis key（`AiModelUsageCounterKey`）/ `AiModelUsageCounterActivatorJob` 的 upsert / `GatewayUsageService`（OpenApi 传 key id 字符串）/ `UsageCapturingChatClient`（传 `appId.ToString()`）以及相关单测同步。

## 鉴权与外部 Token

### 隔离原则

- 外部 token 与内部 JWT 使用**同一 RSA 私钥**签发，但 `aud` 不同：内部为 `SystemOptions.Server`，外部为 `<Server>|<external>`。
- 内部 JWT 校验锁内部 `aud`，**天然拒绝**外部 token；外部端点使用独立认证 scheme，校验外部 `aud` 并把 claims 解析为 `UserContext(UserType=External)`。
- 外部能力全部挂 `/external/...`；内部 Controller 不接受外部 token。保护规则（app 归属、`access_app.app_ids` 命中、`is_auth`）在 Handler/端点鉴权策略中判定。

### Token 获取（`POST /external/token`，匿名）

> **2026-09-14 实现修订**：应外部接入需求，token 体系在原「短时外部 JWT」基础上扩展为 **OAuth 风格双令牌**（`access_token` 2h + `refresh_token` 7d，刷新旋转），并区分**应用 token / 用户 token** 两类主体（应用 token 授权=接入 `app_ids` 全部；用户 token 绑定 `external_user.id` 且以 claim `appid` 圈定**单个应用**）；匿名路径保留原设计。refresh 仅携带主体，**刷新时按库重建授权范围**——接入 `app_ids` 收窄、换绑应用、删除接入（吊销）即时生效。外部 token 与内部 JWT 同钥不同 audience（`Server|external`）双向隔离；`/api/external/*` 受保护端点经 `ExternalJwt` 独立认证方案 + `[ExternalAuthorize]` 拦截器校验。

请求体（二选一）：

- `{ accessAppKey, externalUserId?, nickname? }`：`is_auth=true` 路径。校验 key 存在、未删除；取出该接入的 `team_id/app_ids`。若提供 `externalUserId`，按 `(access_app_id, external_user_id)` upsert `external_user` 行（复用 id → 继承历史）；否则生成临时 `external_user_id` 新建行。
- `{ appId, externalUserId?, nickname? }`：`is_auth=false` 匿名路径。校验 `app.is_external=true && app.is_auth=false`、已发布、未禁用；生成临时 `external_user` 行（`app_id` 记为来源）。

响应：短时外部 JWT（`typ=external`、`sub=external.id`、`team_id`、可选 `access_app_id`、`app_ids`、`external_user_id`），有效期默认 2 小时（可配）。不落库用户信息。

### 外部对话（`/external` 路由组）

- `POST /external/agent/{appId:guid}/session`：创建外部会话。校验 appId 在 token 的 `app_ids` 内（`is_auth=false` 时等于本次 token 的 app）；写入 `AppAgentSession`，`user_type=External`、`create_user_id=external.id`。
- `POST /external/agent/{appId:guid}/chat`：AG-UI SSE 对话。复用 `AppAgentDispatcher`（启动时以独立 agent name 注册同一 dispatcher），端点鉴权要求外部 scheme + `appId ∈ app_ids` + `app.is_external=true`；会话归属按 `external_user.id` 校验。
- `GET /external/agent/{appId:guid}/session/list`、`GET /external/session/{sessionId:guid}/messages`：返回该 `external_user` 身份的会话与消息，实现「继承聊天记录」。
- 内部 `POST /api/agent/{appId}/chat` 不变，且不接受外部 token。

### 公开内部应用（`is_public`）

- `GET /app/public/list`：平台内任意已登录用户可看，返回 `is_external=false && is_public=true && publish_status=1 && is_disable=false` 的应用。
- `CreateAppSession`：非团队成员但 `app.is_public=true` 且已发布 → 允许创建会话；会话列表/消息/重命名/删除按「会话归属用户」放行，不要求团队成员。
- `QueryApp` 详情：`is_public` 应用允许非成员查看基础信息（只读）。

## 权限矩阵

| 操作 | Owner/Admin | Member | 非成员（内部用户） | 外部身份 |
|---|---|---|---|---|
| 创建/编辑内部应用（含 `is_public`） | ✅ | 403 | 404 | — |
| 创建/编辑外部应用 | ✅ | 403 | 404 | — |
| 查看团队内部应用列表 | ✅ | ✅ | 404 | — |
| 查看团队外部应用列表 | ✅ | 403 | 404 | — |
| `access_app` 增删改查 | ✅ | 403 | 404 | — |
| 查看平台公开应用列表 | ✅ | ✅ | ✅ | — |
| 使用公开内部应用（建会话/对话） | ✅ | ✅ | ✅（已登录） | — |
| `/external/token` | — | — | — | 匿名（凭 key 或 `is_auth=false` 的 appId） |
| `/external/...` 对话 | — | — | — | 持有效外部 token，且 app 在授权范围 |

## 后端接口

### 一期：外部应用

| 方法 | 路由 | 说明 |
|---|---|---|
| POST | `/app`（改） | `CreateAppCommand`：`EnableForeign` → `IsExternal`+`IsAuth`+`IsPublic` 并校验组合 |
| PUT | `/app/{id}`（改） | `UpdateAppCommand` 同步字段与组合校验；应用类型不可改 |
| GET | `/app/list`（改） | 固定过滤 `is_external=false`；`AppItem` 增 `IsExternal/IsAuth/IsPublic` |
| GET | `/app/external/list?teamId=` | 外部应用列表（`is_external=true`，Admin+） |
| GET | `/app/public/list` | 平台公开应用列表（任意登录用户） |
| POST | `/app/{id}/publish` 等 | 外部应用同样支持发布/取消发布（外部使用要求已发布） |
| POST | `/app/{id}/session`（改） | 允许非成员对 `is_public` 已发布应用建会话；外部应用禁止内部会话 |

### 二期：应用接入（`AccessAppController`，路由 `access-app`）

| 方法 | 路由 | 说明 |
|---|---|---|
| GET | `/access-app/list?teamId=` | 列表（Admin+），不回显 key 原文，只回显前缀/掩码 |
| POST | `/access-app` | 创建 `{teamId, name, description?, appIds}`；生成 key，**仅在响应返回一次** |
| PUT | `/access-app/{id}` | 改名称/描述/授权 `appIds`（不改 key） |
| DELETE | `/access-app/{id}` | 软删除 |

### 三期：外部 Token 与对话（`ExternalController`，路由 `external_user`）

| 方法 | 路由 | 说明 |
|---|---|---|
| POST | `/external/token` | 见上；匿名 |
| POST | `/external/agent/{appId:guid}/session` | 建外部会话 |
| POST | `/external/agent/{appId:guid}/chat` | AG-UI SSE 外部对话 |
| GET | `/external/agent/{appId:guid}/session/list` | 外部会话列表（按 `external_user.id`） |
| GET | `/external/session/{sessionId:guid}/messages` | 外部会话消息 |

请求模型实现 `IModelValidator<T>` 并写 `static Validate`；枚举带 `JsonPropertyName`；列表 DTO 继承 `AuditsInfo` 并用 `IUserInfoFillService.FillAsync` 填充人名；时间用 `DateTimeOffset`；Guid 用 `Guid.CreateVersion7()`；`BusinessException` 显式设 `StatusCode`；DI 用 Maomi 特性。

## 前端

- **团队管理页**（`TeamManage`）：新增分区 `externalApps`（外部应用）与 `accessApp`（应用接入），加入 `ADMIN_ONLY_SECTIONS`。
  - `TeamExternalApps.tsx`：外部应用卡片/列表 + 新建/编辑（`is_auth` 开关）+ 发布/取消发布。
  - `TeamAccessApps.tsx`：接入 key 列表 + 新建（选择授权外部应用多选）+ 删除；创建后弹「仅显示一次」的 key（参照 `TeamGateway` 密钥弹窗）。
- **内部应用页**（`TeamApps`）：移除「允许外部使用」；新增「公开到平台」(`is_public`) 开关；外部应用不在该列表出现。
- **应用广场**（新页 `/apps`）：任意登录用户可见，展示平台公开应用并可进入对话（复用 `AppChat`）。
- i18n `zh-CN`/`en-US` 同步；新页面同目录 `__tests__`；全部走 `@/design-system`；危险操作 `Popconfirm`；Modal `maskClosable={false}`。

## 失败处理

- `access_app.key` 不存在/已删除 → 401。
- `appIds` 含非本团队或非外部应用 → 400。
- 外部 token 过期/`aud` 不符 → 401；`appId` 不在 `app_ids` → 403。
- 外部应用未发布或已禁用 → 403。
- `is_auth=false` 但该 app 实际 `is_auth=true` → 403（必须走 key 换 token）。
- 用量记账失败不阻断对话返回（沿用现有容错），记 error 日志。

## 验证

- 后端：`dotnet build src/MoAI/MoAI.csproj` 0 error。
- E2E：新增 `local-dev/external-app-e2e.mjs`，覆盖 内部应用 `is_public` 可见/使用、外部应用内外隔离、`access_app` 增删改查与 key 一次性、`is_auth=true` 换 token 与继承、`is_auth=false` 匿名换 token、越权/过期/未授权拒绝。
- 前端：`npm run typecheck && npm run lint && npm run test`。
- 文档：更新 `docs/app/*`，新增 `docs/access-app/*`（或并入 app）与外部用户相关场景；`rounds-log.md` 记账；DDL `asserts/external_app.sql` 并回归脚手架。

## 关键决策

- **D1 字段语义**：`is_external` 唯一区分内外；`is_auth` 仅外部；`is_public` 仅内部；废弃 `enable_foreign`。
- **D2 `access_app` 即应用接入**：授权 key 携带 `app_ids`；本身不含 `is_auth`；key 明文列（沿用用户已建结构），仅在创建响应返回一次。
- **D3 `external_user` 外部用户表**：以 `(access_app_id, external_user_id)` 绑定复用、继承聊天/消费；匿名路径生成临时身份。
- **D4 双 audience 隔离**：外部 token 用独立 `aud`，内部端点天然拒绝；外部能力独立 `/external` 路由组。
- **D5 `is_auth=false` 也走临时 token**：统一身份链路，保证会话与用量可归属；调用方无需 key。
- **D6 用量资源 id 统一为字符串**：顺带修 `ai_model_token_audit.user_id` 为 bigint；不新增 `resource_type`。
- **D7 公开内部应用**：`is_public` + 已发布即可被任意内部用户发现与使用；新增 `/app/public/list` 与放宽 `CreateAppSession`。
- **D8 外部应用复用现有 Agent 运行时**：不重写对话链路，仅补外部鉴权、会话与路由。

## 分期计划

1. **外部应用**：DDL（drop `enable_foreign`）+ 字段/命令/DTO/前端 + 可见性过滤 + 公开列表 + E2E。
2. **应用接入**：`access_app` 四件套 + key 生成/一次性返回 + 前端页 + E2E。
3. **外部 token 与对话**：`external_user` 表 + `/external/token` + `/external` 对话与会话端点 + 用量类型修复 + E2E。

每期独立可验证后再进入下一期。

## 后续迭代

1. 外部 token 吊销控制台与接入用量看板。
2. 外部用户界面（登录态、会话管理）与外部应用独立域名/嵌入 SDK。
3. MCP / OpenAPI 形态的外部应用接入。
4. `access_app` key 轮换与多 key。
5. 外部应用的独立额度与计费。
