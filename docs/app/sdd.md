# 应用管理模块设计规格（SDD）

> 关联：[SDD](./sdd.md) ｜ [BDD](./bdd.md) ｜ [TDD](./tdd.md) ｜ [SOP](./sop.md) ｜ 上游：[../team/sdd.md](../team/sdd.md) ｜ 证据：[local-dev/app-e2e.mjs](../../local-dev/app-e2e.mjs)

- 日期：2026-09-10（2026-09-11 增补：创建/编辑支持头像与外部开关；2026-09-11 增补：应用改卡片展示 + 应用管理页可配置插件/知识库/提示词；2026-09-11 增补：管理页改**单页左右分栏**并支持**对话模型**选择；2026-09-13 增补：**内部/外部应用区分**（`is_external` / `is_auth` / `is_public`）+ 「外部应用」团队分区 + 平台公开应用广场；2026-09-14 增补：**应用工作台**（左侧菜单：配置/日志/监控，外部应用 + 访问点占位）+ **Redis 调试会话**（左配置、右调试，未发布可调试、不落库不计用量）；2026-09-14 增补：**外部 token 体系**（`external_user` 表 + 应用/用户/匿名三类 token + `/api/external` 拦截器，见 §2.2/§4/D30~D33）；2026-09-16 增补：**会话专家提示词**（`app_agent_session.prompt_id` + 绑定接口 + 对话页右侧专家侧边栏，见 §2.1/§5.4 与 @AP-S44/@AP-S45））
- 状态：数据库 + 后端 API + 前端团队内页面（应用卡片列表 + 应用管理页 + 外部应用分区 + 应用广场）已实现；**发布**（`publish_status`/`publish_time`）与会话 CRUD 已实现；Agent 应用的**会话运行**（对话/上下文/知识库 RAG）见 [../ai/sdd.md](../ai/sdd.md)；**外部 token 体系与外部会话/对话端点（/api/external/agent/*）**已实现（D39，见 [外部应用/接入设计](../superpowers/specs/2026-09-13-external-app-and-access-design.md) 与 [访问点设计](../superpowers/specs/2026-09-14-access-point-design.md)）
- 领域：`src/app`（Shared/Core/Api），前端 `ui/src/pages/teams/apps`（团队页「应用」分区 + 应用管理页）
- Schema 真源：库表现状 + `src/database/MoAI.Database.Postgres/Data/App*.cs`（脚手架逆向生成）；原 `asserts/app.sql` / 库表 `app_agent_*` 已随仓库 DDL 清理移除

## 1. 目标

应用是**团队下的产物**，入口也只存在于**团队之内**：进入「团队」→ 某个团队 → 「应用」分区，**团队管理员（Owner/Admin）**在此创建应用并指定应用类型，随后管理/配置应用；**普通成员（Member）进入团队只能使用**——可见分区仅 信息 / 应用 / 知识库，看不到成员、网关、插件、变量、设置这些管理分区，也进不到应用配置。本期交付：应用管理与基础信息（名称、描述、头像、「允许外部使用」）、**应用卡片列表**，以及 **Agent 应用管理页**（左栏基础信息，右栏对话模型、系统提示词、允许使用的插件与知识库）；Agent 应用的会话运行与流程应用的差异化配置留待后续。

> 侧边栏**不再有**一级「应用」菜单（`nav.app`、`/app` 路由与 `ui/src/pages/apps` 页面均已移除）：跨团队的应用聚合页把「管理」放到了团队之外，与「应用属于团队」相悖。

## 2. 数据模型

- `app`：`id(uuid) / name(20) / description(255) / team_id(int) / is_public / is_disable / classify_id / is_external / is_auth / app_type / avatar(255) / publish_status / publish_time / ` 审计四件 + `is_deleted(bigint)`。
- **应用类型** `app_type`：`0=Agent 应用`、`1=流程应用`（`MoAI.Database.Enums.AppType`，成员带 `JsonPropertyName("agent"/"workflow")`，接口出参为字符串）。
- **内部/外部应用（D23）**：`is_external` 唯一区分——`false`=内部应用，`true`=外部应用。
  - 内部应用：团队内使用；`is_public=true` 且已发布时，平台内任意登录用户可在**应用广场**发现与使用。
  - 外部应用：仅外部用户/匿名使用，**内部用户不可见**（`QueryApps` 固定过滤 `is_external=false`）；`is_auth=true` 需经「应用接入」key 换外部 token（下阶段），`false` 则匿名可换临时 token。
  - 组合约束：内部应用 `is_auth` 必须为 false、外部应用 `is_public` 必须为 false；创建/更新时校验（非法组合 400）。
  - **废弃 `enable_foreign`**：已从实体/配置/命令/DTO/前端移除（库列一并删除）。
- 名称在**团队未删除范围内唯一**（应用层校验，重名 409）；`app` 表无 partial 唯一索引（沿用既有表设计，冲突由 Handler 兜底）。
- 审计属性由 `DatabaseContext` 审计钩子自动注入；`is_deleted` 沿用 legacy **bigint**（0=未删除）。
- 与既有 `app_chatapp` / `app_workflow_design` 等 legacy 表无关联，本期不读写它们。

### 2.1 Agent 应用配置与会话（库表 `app_agent_*`）

> 取代 legacy `app_chatapp` / `app_chatapp_chat` / `app_chatapp_chat_history`（三表废弃、不再读写）。
> 实体：`AppAgentConfigEntity` / `AppAgentSessionEntity` / `AppAgentMessageEntity`（`MoAI.Database.Entities`）。

**`app_agent_config`｜Agent 应用配置（与 app 1:1）**

| 列 | 类型 | 说明 |
|---|---|---|
| `id` | uuid PK | 配置ID |
| `team_id` | int | 所属团队，冗余用于团队维度过滤 |
| `app_id` | uuid | 所属应用（→ `app.id`），partial 唯一（`is_deleted = 0`）保证 1:1 |
| `prompt` | varchar(4000) | 系统提示词 |
| `model_id` | **uuid** | 对话模型（→ `ai_model.id`） |
| `wiki_ids` | text | 绑定知识库ID JSON 数组（元素为 `wiki.id`，int），空 `[]` |
| `plugins` | text | 绑定插件ID JSON 数组（元素为 `plugin.id`，uuid 字符串），空 `[]` |
| `execution_settings` | text | 对话参数 JSON 对象（temperature/topP/maxTokens…），空 `{}` |
| 审计四件 + `is_deleted` | | bigint 软删除 |

**`app_agent_session`｜会话列表**

| 列 | 类型 | 说明 |
|---|---|---|
| `id` | uuid PK | 会话ID |
| `team_id` / `app_id` | int / uuid | 归属团队与应用 |
| `title` | varchar(100) | 会话标题，默认「未命名标题」 |
| `prompt_id` | int | 绑定的专家提示词（→ `prompt.id`），**0=未绑定**（2026-09-16 增补，存量库见 `asserts/app_agent_chat.sql`） |
| `user_type` | int | `MoAI.Infra.Models.UserType`：0=识别不到 1=外部用户 2=外部应用 3=内部普通用户 |
| `input_tokens` / `out_tokens` / `total_tokens` | int | 会话 token 累计 |
| `last_message_time` | timestamptz | 最后消息时间，**会话列表按此倒序** |
| `create_user_id` | bigint | 会话归属用户 |
| 审计四件 + `is_deleted` | | bigint 软删除 |

索引：`(app_id)`、`(team_id)`、`(app_id, create_user_id, last_message_time DESC)`。

> **专家提示词（2026-09-16 增补）**：会话可绑定一个提示词作为「专家」。绑定接口 `PUT /app/session/{sessionId}/prompt`（0=清除），创建会话 `POST /app/{appId}/session` 请求体可直接带 `promptId`。可用性规则：本人个人提示词（`team_id=0`）或会话所属团队提示词，其余 404；非归属用户 404；校验失败**不产生会话**。运行期由 `AppAgentDispatcher` 读会话行 `prompt_id` 传入 `AppAgentFactory`，专家内容**追加在应用系统提示词之后**拼成 `ChatOptions.Instructions`（提示词已被删除时静默降级为仅应用提示词）；飞书入口与 Redis 调试会话暂不传专家。场景见 [@AP-S44](./bdd.md#ap-s44)/[@AP-S45](./bdd.md#ap-s45)。

**`app_agent_message`｜会话消息（对话历史）**

| 列 | 类型 | 说明 |
|---|---|---|
| `id` | uuid PK | 消息ID |
| `session_id` | uuid | 所属会话（→ `app_agent_session.id`） |
| `seq` | int | 会话内序号，从 1 递增，**决定顺序**（不依赖时间戳/UUID） |
| `role` | varchar(20) | `system` / `user` / `assistant` / `tool` |
| `content` | text | 正文，空串=无文本（纯工具调用消息） |
| `completions_id` | varchar(50) | 模型一次补全的标识，同次补全多条消息共用 |
| `tool_calls` | text | assistant 请求的插件/函数调用 JSON 数组 |
| `tool_call_id` | varchar(50) | `role=tool` 回填 `tool_calls[].id` |
| `reasoning` | text | 模型推理内容（思维链） |
| 审计四件 + `is_deleted` | | bigint 软删除 |

索引：唯一 `(session_id, seq)`（同时覆盖按会话前缀查询）。

**绑定关系**：`wiki_ids` / `plugins` 在配置表内以 JSON 文本承载（一表搞定，不建关联表）。绑定对象必须是**该团队有权访问**的知识库/插件——校验放在应用层（wiki 按 `team_id`、plugin 按 `plugin.is_public` 或 `plugin_team_authorization` 授权），**不建物理外键**（仓库约定）。

### 2.2 外部用户（库表 `external_user`）与外部 token（D30~D33）

- `external_user`：`id(bigserial)` / `team_id(int)` / `app_id(uuid null, 授权应用)` / `access_app_id(uuid null, 来源接入)` / `external_user_id(varchar128)` / `nickname(varchar100)` / 审计五件套。partial 唯一索引 `(access_app_id, external_user_id) WHERE access_app_id IS NOT NULL AND is_deleted=0`——同一接入下同一外部身份复用同一行（继承会话与消费）；匿名身份（无接入）不受约束。DDL：[asserts/external_app.sql](../../asserts/external_app.sql)。
- **双 audience 隔离（D30）**：外部 token 与内部 JWT 同一 RSA 私钥签发，但 audience 为 `SystemOptions.Server + "|external"`；内部 JwtBearer（aud=Server）天然拒绝外部 token，外部 scheme 也拒绝内部 token——**两类 token 都不能互串**（EA-S1/S3）。
- **三类 token（D31）**：①**应用 token**（`typ=externalapp`）：仅凭 key 签发，主体=接入 id，授权范围=接入 `app_ids` 全部；②**用户 token**（`typ=external`）：key+appId+externalUserId 签发，主体=`external_user.id`，claim `appid` 指定**唯一授权应用**（换绑应用即更新 `external.app_id`）；③**匿名 token**：仅凭 `is_auth=false` 应用 id 签发，生成/复用临时 `external_user` 行。access_token 有效 2h（DEBUG 7d）、refresh_token 7d，响应 `{accessToken, refreshToken, expiresIn, tokenType(app/user), externalId?, externalUserId?}`（long 序列化为字符串）。
- **刷新即重建授权（D32）**：refresh_token 只携带主体（sub/typ），刷新时按库重建 claims——管理员改接入 `app_ids` / 换绑应用 / 删除接入（吊销）在下次刷新即生效；刷新旋转出新 token 对。
- **拦截器（D33）**：`ExternalController` 整体 `[AllowAnonymous]`（规避 convention 自动补 `[Authorize]` 与内部用户态中间件对外部 principal 的误判）+ 独立认证方案 `ExternalJwt`（`ExternalJwtBearerAuthenticationHandler`，AppApiModule 注册）；受保护端点标 `[ExternalAuthorize]`（`IAsyncAuthorizationFilter`，`AuthenticateAsync(ExternalJwt)`，解析出的 `ExternalTokenContext` 放 `HttpContext.Items`）。Handler 不感知 HTTP，授权范围校验以 `ExternalTokenContext.AppIds` 判定。

## 3. 角色与权限矩阵（Handler 层判定，依赖 team_user 事实）


| 操作 | Owner/Admin | Member | 非成员（内部用户） |
|---|---|---|---|
| 列表（内部应用） / 详情 | ✅ | ✅ | 404（公开应用详情除外，见下） |
| 创建（含指定应用类型、头像、`is_external`/`is_auth`/`is_public`） | ✅ | 403 | 404 |
| 更新基础信息（名称/描述/授权与公开开关） | ✅ | 403 | 404 |
| 设置头像（独立接口，编辑时即时生效） | ✅ | 403 | 404 |
| 外部应用列表 `/app/external/list` | ✅ | 403 | 404 |
| 平台公开应用列表 `/app/public/list` | ✅ | ✅ | ✅（需登录） |
| 查看已发布公开应用的详情 | ✅ | ✅ | ✅（`myRole=-1`） |
| 对已发布公开应用建会话 / 对话 | ✅ | ✅ | ✅ |
| 查询 Agent 应用配置 | ✅ | ✅ | 404 |
| 保存 Agent 应用配置（插件/知识库/提示词） | ✅ | 403 | 404 |

> 「非成员」列在**内部应用**上的旧规则是详情 404；新增例外：内部应用 `is_public=true` 且已发布、未禁用时，任意登录用户可只读详情、查自己的会话、建会话并对话。外部应用不进内部列表，走 `/external` 外部链路（下阶段）。

关键规则：

- 应用是团队资源，**创建权仅团队管理员**（区别于知识库：知识库同样是 Admin+ 创建）。
- 应用类型创建后**不可修改**（`UpdateAppCommand` 不含 `AppType`）。
- 非成员访问任意应用接口返回 404；Member 执行写操作返回 403。
- **Agent 应用配置读权限**：配置是「使用应用」的一部分，团队**成员可读**（与列表/详情同级）；写（改绑定与提示词）仍需 Admin+。
- **绑定越权返回 400 而非 403**：`wikiIds`/`plugins` 属于请求体内容而非角色问题——角色不合规是 403，内容是「团队无权使用的资源」是 400（`BusinessException` + `StatusCode=400`）。
- **仅 Agent 应用可配置**：流程应用调用保存返回 400（`app_workflow` 方案未定，不预置配置入口）。

## 4. API 契约（Controller 仅转发，鉴权判定在 Handler）

| 方法 | 路由 | 说明 | 出参 |
|---|---|---|---|
| POST | `/api/app` | 创建应用 `{teamId, name, description?, appType, avatar?, isExternal?, isAuth?, isPublic?}` | `SimpleGuid`（应用 id） |
| GET | `/api/app/list?teamId=` | 团队**内部**应用列表（固定 `is_external=false`，含 myRole、`isExternal/isAuth/isPublic`） | `QueryAppsCommandResponse` |
| GET | `/api/app/external/list?teamId=` | 团队**外部**应用列表（`is_external=true`，Admin+） | `QueryAppsCommandResponse` |
| GET | `/api/app/public/list` | 平台公开应用列表（`is_external=false && is_public && 已发布 && 未禁用`，任意登录用户） | `QueryPublicAppsCommandResponse` |
| GET | `/api/app/{id}` | 应用详情（外部应用对内部用户 404；公开应用非成员可读） | `QueryAppCommandResponse` |
| PUT | `/api/app/{id}` | 更新基础信息 `{name, description?, isExternal?, isAuth?, isPublic?}`（应用类型不可改，`isExternal` 以库内为准） | Empty |
| POST | `/api/app/{id}/avatar` | 设置头像 `{objectKey}`（须为已登记上传文件；编辑态使用） | Empty |
| GET | `/api/app/{id}/agent-config` | 查询 Agent 应用配置（未保存过时返回空配置，不 404） | `QueryAppAgentConfigCommandResponse` |
| PUT | `/api/app/{id}/agent-config` | 保存 Agent 应用配置 `{modelId?, prompt, wikiIds[], plugins[]}` | Empty |
| GET | `/api/access-app/list?teamId=` | 团队应用接入列表（Admin+，回显完整 key，支持再次查看） | `QueryAccessAppsCommandResponse` |
| POST | `/api/access-app` | 创建应用接入 `{teamId, name, description?, appIds[]}`，key 原文仅返回一次 | `CreateAccessAppCommandResponse` |
| PUT | `/api/access-app/{id}` | 更新接入 `{name, description?, appIds[]}`（key 不可改） | Empty |
| DELETE | `/api/access-app/{id}` | 删除接入（软删除） | Empty |
| POST | `/api/external/token` | 换取外部 token：`{accessAppKey}` 应用 token；`{accessAppKey, appId, externalUserId, nickname?}` 用户 token；`{appId}` 匿名 token（需 `is_auth=false`） | `ExternalTokenCommandResponse` |
| POST | `/api/external/token/refresh` | 刷新外部 token `{refreshToken}`，授权范围以库为准重建 | `ExternalTokenCommandResponse` |
| GET | `/api/external/app/list` | 当前外部 token 授权范围内的已发布应用列表（需外部 token，`[ExternalAuthorize]`） | `QueryExternalAuthorizedAppsCommandResponse` |
| POST | `/api/external/agent/{appId}/session` | 外部用户创建会话（需外部**用户** token；应用在授权范围、已发布 Agent 应用） | `SimpleGuid`（会话 id） |
| GET | `/api/external/agent/{appId}/session/list` | 该外部用户在某应用下的会话列表（按最后消息时间倒序） | `QueryExternalAgentSessionsCommandResponse` |
| GET | `/api/external/session/{sessionId}/messages` | 外部会话消息（按 seq 升序；仅归属外部用户且应用在授权范围，否则 404） | `QueryAppSessionMessagesCommandResponse` |
| POST | `/api/external/agent/{appId}/chat` | 外部对话（AG-UI SSE，与内部 `/api/agent/{appId}/chat` 同一 Agent/会话存储）；认证与授权范围由 `ExternalAuthenticationMiddleware` 在管道完成 | SSE 流 |
| GET | `/api/app/{id}/access-point` | 查询访问点配置（内部管理视图，未保存过返回默认值；Admin+） | `AppAccessPointConfigResponse` |
| PUT | `/api/app/{id}/access-point` | 保存访问点配置（整体替换；仅外部应用，Admin+） | Empty |
| GET | `/api/external/app/{appId}/access-point` | 访问点**公开**配置（匿名，悬浮组件用；含 appName/avatarUrl/isAuth/enabled） | `ExternalAccessPointResponse` |

> `modelId` 为 `ai_model.id`（uuid，可空）；传 null/空 Guid 表示不选择模型。`wikiIds` 为 `wiki.id`，`plugins` 为 `plugin.id`。

`QueryAppAgentConfigCommandResponse` 字段：`appId / teamId / appType / prompt / modelId / wikiIds(long[]) / plugins(uuid[]) / myRole`。`modelId` 本期恒为**空 Guid**（模型选择未开放，落库占位）。

**保存配置的校验链**（`SaveAppAgentConfigCommandHandler`，顺序固定）：

1. 应用存在（否则 404）→ 团队角色（非成员 404、Member 403）；
2. 应用类型必须是 Agent（否则 400）；
3. `wikiIds`：逐个比对 `wiki.team_id == app.team_id`，越权 400；
4. `plugins`：`plugin.team_id == teamId`（团队自有）**或** `plugin.is_system && team_id == 0 && (is_public || plugin_team_authorization 已授权本团队)`，越权 400；
5. `prompt` 最长 4000（`IModelValidator`，超长 400，先于 Handler 执行）。

校验在写入前完成，失败时**不产生任何写入**（E2E AP-17d 验证）。`WikiIds`/`Plugins` 在库内是 JSON 数组文本，读写经 `AppAgentConfigJson` 收口（非法 JSON 解析为空列表而非抛异常）。

- 路由经 `ApiApplicationModelConvention("/api")` 统一加 `/api` 前缀。
- `{id}` 为 Guid；应用 id 在响应中序列化为字符串（Kiota 侧 `byId(id: string)`）。
- 命令的用户上下文经 `IUserIdContext` 由 `AutoAssignUserIdFilter` 自动注入，属性加 `[JsonIgnore]` 不进入接口文档。
- **创建时带头像**：应用 id 要到创建成功才生成，无法先调 `/{id}/avatar`，因此 `avatar` 是创建请求体字段；服务端对它做与头像接口**完全相同**的登记校验（未登记/未完成上传 → 404）。

## 5. 前端设计（团队页「应用」分区）

- **入口唯一**：`/team/:id/apps`，即团队页左侧分区菜单的「应用」（`SectionKey = 'apps'`）。组件 `ui/src/pages/teams/apps/TeamApps.tsx`，与 `TeamWikis` / `TeamPlugins` 同构；`ui/src/api/app.ts` 为唯一封装层。
- 分区菜单顺序：信息 / **应用** / **外部应用** / 成员 / 模型网关 / 知识库 / 插件 / 环境变量 / 设置；默认落在「信息」。
- **角色可见性（前端渲染层）**：
  - Owner/Admin：全部分区可见。
  - Member：只保留 信息 / 应用 / 知识库；**外部应用、成员、模型网关、插件、环境变量、设置被隐藏**，直接改 URL 访问也会回落到「信息」。
  - 该收敛只影响渲染，真正的门禁仍在后端 Handler（Member 写操作 403、非成员 404）。
- 分区内容（收窄为**单团队**）：**卡片网格**（`Row`/`Col`，与 `/wiki` 卡片同构）——卡片含 头像 + 名称 + 类型标签，描述（2 行省略），底部一行 创建时间 + 发布状态 + 「公开到平台」状态标签（已公开/未公开，`isPublic`）；**卡片右上角「管理」**（仅 `canManage` 渲染）进入应用管理页。列表顶部左侧为说明文案（管理员为「卡片右上角管理」提示，Member 为只读说明），右侧为「新建应用」。
- **「外部应用」分区**（`TeamExternalApps.tsx`，仅 Owner/Admin 可见）：列表展示 `isExternal=true` 的应用，卡片底部展示 发布状态 + 「需授权/免授权」(`isAuth`)；支持新建（含头像与 `isAuth` 开关）、发布/取消发布，并可进入应用管理页配置模型/提示词/插件/知识库。内部用户的应用分区看不到这些应用。
- 新建弹窗（内部应用）：应用类型 + **头像** + 名称 + 描述 + **「公开到平台」开关**（`isPublic`）；**团队由所在分区确定，不再选团队**。外部应用分区的新建弹窗则为「需要授权访问」开关（`isAuth`），且固定 `isExternal=true`。
  - 头像「先直传、后提交」：选图即走 `uploadImageWithKey` 拿 `objectKey` 并在弹窗内预览，点确定时随创建请求一起提交。取消/关闭弹窗会丢弃已选头像，已上传的对象成为孤儿文件（与仓库其他「先传后用」场景一致，暂不做清理）。
- Member 视图：只读卡片，顶部一行说明「只能查看与使用应用；创建与配置需要团队管理员」，不渲染新建按钮与「管理」入口。
- 文案走 `t()`，zh-CN / en-US 同步（`appManage.*`、`team.apps`）。
- 头像经 `resolveStorageUrl` 转可访问地址（同团队/知识库头像）。

### 5.1 应用管理页（`ui/src/pages/teams/apps/AppManage.tsx`）

- **路由**：`/team/:teamId/app/:appId`（**无分区参数**）；页面为**单页左右分栏**（`Page` 面包屑 + `Row`/`Col`），**左侧「应用信息」、右侧「Agent 配置」**，不做左侧菜单/分区切换。
- **左栏 应用信息**（`DSCard` 标题「应用信息」）：头像（选中即上传生效，走 `POST /{id}/avatar`）+ 类型（只读标签）+ 名称 + 描述 + **（内部）「公开到平台」/（外部）「需要授权访问」开关** + 「保存信息」（`PUT /{id}`）。即原编辑弹窗改为页内表单，`TeamApps` 不再有编辑入口。
- **右栏 Agent 配置**（`DSCard` 标题「Agent 配置」）：对话模型 + 提示词 + 允许使用的插件 + 允许使用的知识库 + 「保存配置」（`PUT /{id}/agent-config`）。四个字段**同一份配置状态、同一个保存按钮**，一次提交完整配置。
  - **对话模型**：`Select`（单选、可清空），选项来自 **`GET /api/team/{id}/gateway/models`**（团队可用网关模型：公开模型 + 已授权本团队的私有模型）——与团队「模型网关」分区同源，**取值范围天然等于「该团队有权使用」**。空值表示不指定模型（落空 Guid）。
  - **插件 / 知识库**：`Select mode="multiple"`，选项来自 **`GET /api/team/{id}/plugin/list`**（团队可访问插件）与 **`GET /api/wiki/list?teamId=`**（本团队知识库）；`PluginId` 为空 Guid 的内存静态插件（无 DB 记录）在展示层被过滤，不可绑定。
  - **提示词**：`Input.TextArea`（maxLength 4000 + `showCount`）。
- **流程应用**：右栏**不渲染配置项**，只展示「流程应用的配置能力尚未开放」提示；左栏仍是完整基础信息。
- **Member 访问**：页面渲染为只读（表单 `disabled`、无保存按钮）并顶部提示；后端仍以 403 兜底。

### 5.2 应用广场（`ui/src/pages/apps/AppPlaza.tsx`，路由 `/apps`）

- 侧边栏一级「应用广场」（`nav.apps`），任意登录用户可见；数据来自 `GET /api/app/public/list`（跨团队公开内部应用）。
- 卡片展示 头像/名称/类型/描述 + 所属团队；Agent 应用提供「进入对话」，跳 `/team/:teamId/app/:appId/chat`（非成员凭公开应用可建会话、对话）。
- 文案 `appPlaza.*`、`nav.apps`，zh-CN / en-US 同步。

### 5.3 应用接入（`ui/src/pages/teams/apps/TeamAccessApps.tsx`，团队页「应用接入」分区）

- 分区菜单顺序：信息 / 内部应用 / 外部应用 / **应用接入** / 成员 / 模型网关 / 知识库 / 插件 / 环境变量 / 设置；「应用接入」仅 Owner/Admin 可见。
- 列表：接入名称、**key（明文，回显完整 key；前端默认掩码，点击展开 / 复制）**、授权的外部应用标签、创建时间；支持新建 / 编辑 / 删除（`Popconfirm`）。
- 新建/编辑弹窗：名称（≤20）+ 描述（≤255）+ 授权外部应用多选（选项来自 `GET /app/external/list`，即本团队外部应用）；创建后弹「接入 key」窗口，列表中可再次查看/复制。
- 封装层 `ui/src/api/access-app.ts`；文案 `accessApp.*`、`team.accessApps`，zh-CN / en-US 同步。

### 5.4 应用工作台（`ui/src/pages/teams/apps/AppWorkspace.tsx`，路由 `/team/:teamId/app/:appId/:section?`）

- **左侧菜单**：`config`（配置）/ `logs`（日志）/ `monitor`（监控）；外部应用（`is_external=true`）额外 `access`（访问点）。采用与 `WikiDetail`/`TeamManage` 一致的 `Layout` + `Sider` + `Menu`，菜单项带图标（Setting/Profile/AreaChart/Api）；`section` 非法或缺省回落 `config`。原 `AppManage.tsx` 单页左右分栏已删除，配置内容迁入 `AppConfigSection`。
- **分区可见性（前端渲染层）**：`logs`/`monitor` 仅团队 Owner/Admin 可见；`access` 仅 Owner/Admin 且外部应用可见；Member 只有 `config`（只读，无调试），深链 `/logs` 回落 `config`。
- **配置分区**（`AppConfigSection.tsx`）：左栏（`lg=15`）为 应用信息 + Agent 配置（含沙箱）；右栏（`lg=9`）为**调试对话** `AppDebugChat`（仅 Agent 应用且 Admin+；否则提示）。保存信息/头像/发布后**静默刷新**（`load(true)`），不卸载调试面板。
- **调试会话（Redis 临时会话）**：`POST /api/app/{id}/debug/session`（Admin+）生成 `Guid.CreateVersion7()` 会话 id 并写 Redis 注册表 `appagent:debug:{id}`（TTL 2h 滑动）；对话仍走 `/api/agent/{appId}/chat`，`AppAgentDispatcher` 在无 `app_agent_session` 行时回落注册表（校验 `UserId`），以 `isDebug=true` 装配（跳过 `UsageCapturingChatClient`）；`AppChatFlushService.FlushAsync` 无 session 行即 return → 调试对话**不落库**。前端刷新即弃用会话 id。
- **日志分区**（`AppLogsSection.tsx`，Phase 2 已交付）：`GET /api/app/{id}/logs`（Admin+，分页，支持 标题关键字 / 用户类型 / 最后消息时间范围 过滤；数据源为全用户的正式会话 `app_agent_session`，即压缩后视图）+ `GET /api/app/{id}/logs/{sessionId}/messages`（Admin+，按 `seq` 返回该会话消息）。列表条目 `AppLogItem : AuditsInfo`，内部用户人名由 `IUserInfoFillService.FillAsync` 填充，外部用户按 `userType` + `ownerId` 展示（不填内部人名）。前端 `DataTable` + 详情 `Drawer`。
- **监控分区**（`AppMonitorSection.tsx`，Phase 3 已交付）：`GET /api/app/{id}/usage`（Admin+），返回用量汇总（调用次数 / 输入 / 输出 / 合计 token）与按模型分布。数据源为聚合表 `ai_model_token_audit`（`UseType=App` + `use_resource_id == appId` Guid + `team_id`），**最多滞后约 1 分钟**；调试会话不计数。本期**不含按日趋势**（聚合表无时间分桶）。
- **对话页专家侧边栏（`AppChat.tsx`，2026-09-16 增补）**：顶栏「专家」按钮（`UserSwitchOutlined`，绑定时带 Badge 圆点）点击后右侧浮层展开专家列表 = 本人个人提示词（`getMyPrompts`）+ 所在团队提示词（`getTeamPrompts`），支持关键字本地过滤与 个人/团队 来源标签。点选即绑定：已有会话直接调 `PUT /app/session/{id}/prompt`，未发送过消息则暂存本地、首轮 `createAppSession` 随会话一并创建；再点同一项取消。选中专家在输入框上方以提示条展示（可点 × 清除）；切换会话按该会话 `promptId` 回显。
- **访问点**：仍为占位，Phase 4 交付。

## 6. 关键决策

- **D1 应用类型语义**：`0=Agent 应用`、`1=流程应用`；同步修正 `app.app_type`、`AppEntity`、`AppConfiguration` 的旧注释（原为「普通应用/流程编排」）。
- **D2 创建权归团队管理员**：应用是团队下的产物，与知识库/插件一致按团队角色门禁；Member 只读。
- **D3（修订）本期交付基础信息 + 头像 + 授权/公开开关**：名称/描述/头像与 `is_external`/`is_auth`/`is_public` 开关在**创建与编辑**时都可设置。**仍未开放**：启用/禁用（`is_disable`）、分类绑定（`classify_id`）、删除、列表分页与筛选。
- **D4（修订）Agent 应用的配置/会话表已落地**：`app_agent_config` / `app_agent_session` / `app_agent_message`（库表 `app_agent_*`），取代 legacy `app_chatapp*`。**流程应用**的配置表仍暂缓，待流程引擎方案确定后再设计，避免先建表后返工。
- **D5 头像防伪造**：`objectKey` 必须是 `file` 表中 `is_uploaded = true` 且未删除的记录，否则 404。
- **D6 用户上下文经 IUserIdContext**：Handler 不注入 `IUserContextProvider`（仓库铁律）；上下文属性加 `[JsonIgnore]`，避免 `ContextUserId` 出现在 OpenAPI 查询参数/请求体中。
- **D7 team_id 类型**：表列为 `int`（既有设计），命令层用 `long` 与 `ITeamService.GetMyRoleAsync` 对齐，写入时收敛。
- **D8 表名与语义**：legacy `app_chatapp`（注释「普通应用」）→ `app_agent_config`（「Agent 应用配置」），一张表承载 Agent 应用的应用级配置。
- **D9 `model_id` 用 uuid**：对齐 `AiModelEntity.Id`（Guid）。legacy `app_chatapp.model_id` 为 `integer`，属旧库遗留口径，**本模块不复用**；同理 `app_id` 为 uuid（`app.id`）、`wiki_ids` 元素为 int（`wiki.id`，注意与 legacy 的 bigint 假象区分）。
- **D10 绑定关系用 JSON 文本而非关联表**：`wiki_ids` / `plugins` 直接存 JSON 数组（一表搞定，读写原子）。代价是无法用 SQL 反查「哪些应用绑定了某插件」，需要时按需再拆关联表；绑定对象的**团队可访问性校验放应用层**，不建物理外键。
- **D11 消息顺序用 `seq`**：`app_agent_message.id` 是 `uuid_generate_v4()`（非时序），同一请求内 user/assistant 两条消息的 `create_time` 可能相同，因此**不依赖 id/时间戳排序**，用会话内自增 `seq`，并加唯一索引 `(session_id, seq)`。
- **D12 `execution_settings` 为 JSON 对象**：存 `{}`（temperature/topP/maxTokens 等键值）。legacy 默认写成 `'[]'` 系误用；`wiki_ids` / `plugins` 则统一为 JSON 数组 `[]`，两者语义不同、默认值不同。
- **D13 会话归属与外部访问预留**：`app_agent_session.create_user_id` 为会话归属用户，`user_type` 对齐 `MoAI.Infra.Models.UserType`，为「外部用户使用应用」预留；当前仅内部用户（`Normal=3`），不实现对外访问。
- **D14（入口修正）应用管理只在团队内，成员只能用**：移除侧边栏一级「应用」与 `/app` 页面，改为团队页的「应用」分区（`/team/:id/apps`）。原跨团队聚合页把管理放到团队之外，方向错了。同时按角色收敛团队页分区——Member 只保留 信息 / 应用 / 知识库，管理分区与应用配置对成员不可见（URL 直达也回落信息页）；后端权限不变（Member 403 / 非成员 404），前端只是不渲染。
- **D15（创建带头像）头像先直传、再随创建请求提交**：应用 id 要到创建成功才生成，无法先调 `POST /{id}/avatar`。因此在新建弹窗内选图即走存储直传拿 `objectKey`，点确定时作为 `CreateAppCommand.Avatar` 一并提交；服务端对 `avatar` 执行与头像接口**完全相同**的「必须已登记上传」校验（伪造 → 404）。这样避免了「先建空应用再补头像」的两段式失败态（应用建了但头像失败）；编辑态仍走独立的 `POST /{id}/avatar`（选中即生效）。
- **D16（已废止，被 D23 取代）**：原 D16 决定「允许外部使用」写 `enable_foreign`。该列已删除，统一改用 `is_external` / `is_auth` / `is_public`，见 D23。
- **D17（列表改卡片 + 管理页收口）**：应用列表由 `DataTable` 改为**卡片网格**，卡片右上角「管理」进入独立管理页；原编辑弹窗的职责（基础信息）并入管理页，`TeamApps` 只保留创建。理由：卡片更适合应用这类带图标的对象，且把「基础信息 + 资源配置」收口到一处，避免列表页同时承载编辑与配置两条入口。
- **D18（配置读权限给成员）**：`GET /agent-config` 允许团队 **Member** 读（配置属于「使用应用」的一部分，与列表/详情同级），写仍限 Admin+。若后续需要隐藏提示词，再按角色裁剪响应字段即可，不改接口形状。
- **D19（绑定校验在应用层，越权 400）**：`wiki_ids`/`plugins` 取值为「该团队有权使用」的资源，校验放在 `SaveAppAgentConfigCommandHandler`（wiki 按 `team_id`、plugin 按团队自有或系统公开/已授权），**不建物理外键**（沿用 D10）；内容越权返回 **400**（区别于角色的 403），且校验先于写入，失败不落库。
- **D20（配置保存为整体替换）**：管理页所有配置项共用一个 `PUT /agent-config`，提交时携带完整 `{modelId, prompt, wikiIds, plugins}`。不做「按字段局部更新」的接口，避免并发/漏字段导致绑定被静默清空。
- **D21（管理页不做左侧菜单，单页左右分栏）**：初版管理页照 `WikiDetail` 用「左 `Menu` 分区 + 右 `Content`」，把基础信息/插件/知识库/提示词拆成四个分区。实际使用中配置项只有四项、且互相有关联（模型 + 提示词 + 插件 + 知识库共同定义一次对话），分区切换反而要来回跳，于是**去掉左侧菜单**，改为**一页左右两栏**：左栏应用信息、右栏 Agent 配置。路由随之简化为 `/team/:teamId/app/:appId`（去掉 `:section?`），页面内不再有 URL 级的子状态。
- **D22（对话模型取值范围 = 团队可用网关模型）**：`modelId` 写 `ai_model.id`（D9），取值来源**复用团队「模型网关」的既有查询** `GET /api/team/{id}/gateway/models`（公开模型 + `ai_model_authorization` 已授权本团队的私有模型，且模型与渠道均启用），不在应用模块另造一份模型可见性逻辑。服务端同样按这套口径复核（`SaveAppAgentConfigCommandHandler.ValidateModelIdAsync`）：模型不存在/未启用/渠道停用 → 400，私有模型未授权本团队 → 400。**未选模型允许**（落空 Guid），此时是否回落到默认模型属于会话运行阶段的事。
- **D23（`is_external` 唯一区分内外，废弃 `enable_foreign`）**：`false`=内部应用、`true`=外部应用；`is_auth` 仅外部应用有意义（是否需要应用接入授权），`is_public` 仅内部应用有意义（是否公开到平台）。创建/更新校验组合：内部应用 `isAuth=true` 或外部应用 `isPublic=true` → 400。旧列 `enable_foreign` 从库与代码移除。
- **D24（内部应用可见性隔离 + 公开广场）**：`QueryApps` 固定过滤 `is_external=false`（内部用户看不到外部应用）；外部应用走独立 `/app/external/list`（Admin+）。内部应用 `is_public && 已发布 && !is_disable` 时，`GET /app/public/list` 对任意登录用户可见，非成员可读详情（`myRole=-1`）、查/建自己的会话并对话；外部应用禁止走内部会话。
- **D25（外部应用在团队内管理）**：外部应用仍属团队、由团队 Admin+ 在「外部应用」分区创建与配置，复用同一套 `app_agent_config`；是否可被外部访问由 `is_auth` 与发布状态决定，实际外部访问链路（应用接入 key、外部 token、`/external` 端点）为下阶段（见设计文档）。
- **D26（应用接入放在 app 模块）**：`access_app` 的 CRUD 直接放 `MoAI.App`（Shared/Core/Api），不新开模块；原因：它只服务应用、依赖 `app.is_external`，且单表 CRUD，独立模块的装配成本不划算。key 明文列存储，创建时生成并在创建响应返回，**列表中回显完整 key（支持再次查看，前端默认掩码、点击展开/复制）**；授权 `appIds` 必须是本团队的外部应用，否则 400。**key 换外部 token 的链路（Phase 3）尚未实现**。

- **D27 工作台分区用左侧菜单**：修订原 D21「管理页不做左侧菜单」——分区由 1 个增至 4 个后必须有导航，采用与 `WikiDetail`/`TeamManage` 一致的 `Layout` + `Sider` + `Menu`，配置分区内部再左右分栏（左配置、右调试）。
- **D28 调试会话复用现有对话链路 + Redis 注册表**：不新建 AG-UI 端点与 store；dispatcher 在 DB 无会话行时回落 `IDebugSessionRegistry` 解析，判定归属后按调试装配。
- **D29 调试不落库靠「无 session 行」自然成立**：`AppChatFlushService.FlushAsync` 查不到会话行即 return，无需新增 `is_draft` 字段；`isDebug=true` 时工厂跳过用量计数器（不计用量）。
- **D30（双 audience 隔离）**：外部 token audience = `Server|external`（`ExternalAuthDefaults.BuildAudience`），与内部 JWT（aud=Server）同钥不同 aud，双向天然拒绝；这是「外部 token 不能访问正常接口」与「内部 token 不能访问外部接口」的唯一守卫，无需逐接口判断。
- **D31（应用/用户/匿名三类 token 统一出口）**：均由 `ExternalTokenProvider`（接口在 App.Shared，实现 `[InjectOnScoped]` 于 Core）签发；应用 token 对象是接入本身（不落 external 行），用户/匿名 token 主体是 `external_user.id`。用户 token 用 claim `appid` 圈定**单应用**授权（用户要求「只能访问一个应用」），授权判断统一走 `ExternalTokenContext.AppIds`。
- **D32（refresh 旋转 + 库内重建授权）**：refresh_token 无状态 JWT 只存主体；每次刷新查库恢复最新授权并签发新 token 对——接入的 `app_ids` 收窄、外部用户换绑应用、接入被删（吊销）都即时反映。access token 剩余有效期内的已签发凭据不做即时吊销（无状态 JWT 的既有取舍，见已知问题）。
- **D33（/api/external 拦截器形态）**：采用与网关一致的「`[AllowAnonymous]` + 显式认证」而非 `[Authorize(AuthenticationSchemes=...)]`——因为 `ApiApplicationModelConvention` 会给无 `[AllowAnonymous]` 的端点自动补默认 scheme 的 `[Authorize]`，且 `CustomAuthorizaMiddleware` 只认内部 JWT 的用户态。外部身份经 `ExternalTokenContext`（claims 解析产物）显式传给 Handler，不注入 `IUserContextProvider`。
- **D39（外部会话复用内部派发链路）**：`ExternalAuthenticationMiddleware`（注册于 `CustomAuthorizaMiddleware` 之前）对 `/api/external` + Bearer 用外部 scheme 认证并把 `external_user.id` 写入 `ClaimTypes.NameIdentifier`——`UserContextProvider` 沿用内部解析得到 `UserId=external_user.id`，`AppAgentDispatcher` 的「会话归属 = CreateUserId」校验与 `AppAgentFactory` 装配**零改动**复用（会话行 `user_type=External`、`create_user_id=external_user.id`）。外部对话端点在 `AppAgentEndpointMapper` 挂同一 `AgentName`（同 `AppAgentSessionStore`）；AG-UI 端点**不经 MVC `/api` 前缀 convention**，模板需写完整路径。应用 token（`typ=externalapp`）无数值主体，`NameIdentifier` 固定 0，不能发起会话等用户级操作（403）。
- **D30 调试会话 TTL 独立**：注册表 2h 滑动续期；前端刷新即弃用 id；残留热态键靠既有 24h TTL 清理。
- **D31 日志为压缩后视图**（Phase 2）：不做原始消息留存；外部身份按 `user_type` 区分展示。
- **D32 监控基于聚合用量表（无趋势）**（Phase 3 已交付）：直接查 `ai_model_token_audit`（`UseType=App` + `use_resource_id == appId`，该列为 Guid，无需 D6 字符串化迁移）交付汇总 + 按模型分布；**不含按日趋势**（聚合表逐维一行、无时间分桶），趋势需逐次用量日志或按日聚合，留后续。
- **D33 访问点本期占位**（Phase 4）：仅外部应用可见，先定地址形态与授权口径。

## 7. 已知问题 / 下阶段

- **外部应用的实际访问链路未实现**：`is_external`/`is_auth` 已落库并可管理，但「应用接入 key」「外部用户 token」「`/external` 对话端点」分别为设计文档的第 2、3 期，尚未交付。当前外部应用只能被创建/配置/发布，不能真正被外部调用。
- 新建弹窗取消或创建失败时，已直传的头像对象会成为**孤儿文件**（无引用、无清理）。
- **库列注释与代码语义不一致**：`app.app_type` 的库注释仍是旧的「普通应用=0,流程编排=1」，而代码/文档已按 `AppType`（Agent=0 / 流程=1）执行。由于 `AppEntity` / `AppConfiguration` 由脚手架**按库注释逆向生成**，只改 C# 注释会在下次重跑时被覆盖——要彻底一致需先改库列注释（`comment on column app.app_type is ...`）再重跑脚手架。
- 应用删除、启用/禁用、分类绑定、列表分页与关键字筛选未实现；当前列表为团队维度全量。
- **「使用」应用尚无动作**：Member 在应用分区只能看到只读卡片——打开应用/对话依赖 Agent 运行时（会话），属下阶段；当前不放置无效的「打开」按钮。
- 应用列表未展示创建人（未接入 `IUserInfoFillService`）。
- **Agent 应用配置已可读写，但尚未运行**：`app_agent_config` 的 `model_id`/`prompt`/`wiki_ids`/`plugins` 已有读写接口与管理页；**`execution_settings` 仍无设置入口**（新建配置行时落 `{}`，temperature/topP/maxTokens 等默认值由运行时决定），会话（`app_agent_session`/`app_agent_message`）与对话运行未实现。**未选模型是合法状态**（空 Guid），「不指定模型时用什么」留待会话运行阶段定义。
- **绑定无反向查询**：`wiki_ids`/`plugins` 为 JSON 文本，删除插件/知识库时不会级联清理引用（D10 的代价）；已绑定的资源被删除后，管理页会显示一个无法解析为选项的 id（表现为裸 id 标签）。**模型同理**：模型被停用/取消授权后，已保存的 `model_id` 不会自动清空，管理页会显示空标签（选项已不在列表里）。
- 流程应用的应用级配置（`app_workflow_design` 等）未实现；管理页对流程应用只开放基础信息。
- 消息表未落 `status`（生成中/完成/失败）与逐条 token；会话表已按会话维度累计 token。
- `app` 表未建 partial 唯一索引，并发创建同名应用存在极小概率穿透（Handler 先查后写）。
- **访问点组件走查待做**：工作台 `access` 分区（配置表单/端点/嵌入片段）与 `/embed/moai-widget.js` 已交付；浏览器真实悬浮对话走查（需配置桩模型渠道）随 4c 收尾。
- **外部对话端点的会话范围守卫在中间件**：`ExternalAuthenticationMiddleware` 对 `/api/external/agent/{appId}/chat` 校验 `appId ∈ token 授权范围` + 应用可用（已发布、未禁用）；对话中未配模型时派发器以 SSE 文本返回装配错误（HTTP 200），不计费。真实模型对话的端到端 E2E（需自建桩模型与渠道配置）随访问点 4c 交付。
- **外部 access token 无即时吊销**：吊销接入/换绑应用依赖 refresh（≤7d）或 access 自然过期（2h）；期间已签发 access token 仍有效（无状态 JWT 取舍）。
- **监控无按日趋势**：用量数据来自聚合表 `ai_model_token_audit`，无时间分桶，无法画日趋势；趋势需逐次用量日志或按日聚合表，留后续迭代。
- **监控最多滞后约 1 分钟**：应用对话只累加 Redis 计数器，由 Hangfire 每分钟 flush 到 `ai_model_token_audit`。
- **日志为压缩后视图**：`app_agent_message` 只保留压缩后视图，被压缩掉的历史原文不可回溯；调试会话不落库，故不出现在日志中。
- **日志仅 Admin+**：Member 查看返回 403、非成员 404；`AuditsInfo.CreateUserId` 为 `int`，外部用户 `long` id 仅作 `ownerId` 原样返回（非内部人名）。
- **调试会话残留热态**：调试会话在 Redis 的消息/快照沿用 24h TTL，注册表 2h 过期后不可再解析，键随 TTL 自然清理；不做服务端即时销毁。
- **调试会话归属仅按 UserId 校验**：注册表未存 `UserType`，与正式会话的归属校验等价；如后续需唯一用户类型可扩展。
