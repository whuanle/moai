# 应用管理模块设计规格（SDD）

> 关联：[SDD](./sdd.md) ｜ [BDD](./bdd.md) ｜ [TDD](./tdd.md) ｜ [SOP](./sop.md) ｜ 上游：[../team/sdd.md](../team/sdd.md) ｜ 证据：[local-dev/app-e2e.mjs](../../local-dev/app-e2e.mjs)

- 日期：2026-09-10（2026-09-11 增补：创建/编辑支持头像与外部开关；2026-09-11 增补：应用改卡片展示 + 应用管理页可配置插件/知识库/提示词；2026-09-11 增补：管理页改**单页左右分栏**并支持**对话模型**选择；2026-09-13 增补：**内部/外部应用区分**（`is_external` / `is_auth` / `is_public`）+ 「外部应用」团队分区 + 平台公开应用广场；2026-09-14 增补：**应用工作台**（左侧菜单：配置/日志/监控，外部应用 + 访问点占位）+ **Redis 调试会话**（左配置、右调试，未发布可调试、不落库不计用量）；2026-09-14 增补：**外部 token 体系**（`external_user` 表 + 应用/用户/匿名三类 token + `/api/external` 拦截器，见 §2.2/§4/D30~D33）；2026-09-16 增补：**会话专家提示词**（`app_agent_session.prompt_id` + 绑定接口 + 对话页右侧专家侧边栏，见 §2.1/§5.4 与 @AP-S44/@AP-S45）；2026-09-17 增补：**对话开场白**（`app_agent_config.opening_statement/-enabled` + 应用详情下发 + 聊天页/调试面板新会话展示，见 §2.1/§5.4 与 @AP-S46/@AP-S47）；2026-09-19 增补：**应用默认技能**（`app_agent_config.skills` 改默认语义，用户技能勾选仅限默认范围，见 [../skill/sdd.md](../skill/sdd.md) §9）与**工作台「信息」分区**（Agent 应用左侧菜单新增，基础信息自配置分区拆出，见 §5.4 与 @AP-S49）；2026-09-20 增补：**发布配置快照双轨**（`published_config`/`status` 快照列，发布即快照、保存只落草稿，正式会话/详情/落库压缩按快照执行，见 §2.1 与决策 D42、@AP-S54）；2026-09-20 增补：**对话附件**（输入卡上传文档/图片 → Maomi.ToMarkdown 提取注入消息文本，见 §4 与 §5.4、@AP-S55/@AP-S56）；2026-09-20 增补：**快捷输入**（`app_agent_config.quick_inputs` 管理员自定义 ≤10 条，应用详情随开场白下发，聊天页欢迎态点击即发送；输入框默认三行、移除欢迎态副标题，见 §2.1/§5.4、决策 D43 与 @AP-S57/@AP-S58）；2026-09-20 增补：**重新发布入口**（已发布且有草稿变更时，工作台头部主按钮 + 配置区警告条 action，复用发布接口把草稿推入快照，见 §5.4 与 @AP-S59）；2026-09-20 增补：**专家选择并入应用设置**（右上角独立专家入口移除，设置面板加宽至 380px、分区重排为 专家→技能→工具审批，会话内切换专家改经设置保存生效，见 §5.4 与 @AP-S44）；2026-09-20 增补：**图片附件多模态注入**（图片不再只以链接文本附带，后端 `ChatAttachmentImageChatClient` 装饰器在发往模型前把图片标记块转为 image/* DataContent 字节内联，图片块格式改带 objectKey 属性 + 裸下载地址，见 §4 与 §5.4、@AP-S64））
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
| `workflow_apps` | text | 绑定为工具的流程应用ID JSON 数组（元素为 `app.id`，uuid 字符串，须为本团队已发布流程应用），空 `[]`（2026-09-20 增补，存量库见 `asserts/app_agent_workflow_apps.sql`，决策 D44） |
| `skills` | text | 应用默认使用的技能ID JSON 数组（元素为 `skill.id`，uuid 字符串），用户可在应用设置中取消勾选（语义见 [../skill/sdd.md](../skill/sdd.md) §9） |
| `execution_settings` | text | 对话参数 JSON 对象（temperature/topP/maxTokens…），空 `{}` |
| `opening_statement` | varchar(4000) | 对话开场白内容，空 `''`（2026-09-17 增补，存量库见 `asserts/app_agent_opening_statement.sql`） |
| `opening_statement_enabled` | boolean | 是否启用对话开场白，默认 `false` |
| `quick_inputs` | text | 快捷输入列表 JSON 数组文本（管理员自定义，聊天页欢迎态点击即发送），空 `[]`（2026-09-20 增补，存量库见 `asserts/app_agent_quick_inputs.sql`，决策 D43） |
| `published_config` | text NULL | 发布配置快照 JSON（camelCase 全字段），发布时写入；null=从未发布，存量已发布应用回退实时配置（2026-09-20 增补，存量库见 `asserts/app_agent_published_config.sql`，决策 D42；流程应用仅开场白有意义，随流程发布一并快照） |
| `status` | smallint | 配置状态，0=草稿有未发布变更 1=草稿与已发布一致（2026-09-20 增补） |
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
| GET | `/api/app/{id}` | 应用详情（外部应用对内部用户 404；公开应用非成员可读）；Agent 应用随详情下发 `openingStatement`/`openingStatementEnabled`/`quickInputs`（成员可读，聊天页开场白与快捷输入取值点） | `QueryAppCommandResponse` |
| PUT | `/api/app/{id}` | 更新基础信息 `{name, description?, isExternal?, isAuth?, isPublic?}`（应用类型不可改，`isExternal` 以库内为准） | Empty |
| POST | `/api/app/{id}/avatar` | 设置头像 `{objectKey}`（须为已登记上传文件；编辑态使用） | Empty |
| GET | `/api/app/{id}/agent-config` | 查询 Agent 应用配置（未保存过时返回空配置，不 404） | `QueryAppAgentConfigCommandResponse` |
| PUT | `/api/app/{id}/agent-config` | 保存 Agent 应用配置 `{modelId?, prompt, wikiIds[], plugins[], openingStatement?, openingStatementEnabled?, quickInputs?}` | Empty |
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
| POST | `/api/app/session/{sessionId}/tool-approval` | 对会话中挂起等待人工审批的工具调用做出决策 `{toolName, approved}`；仅会话归属用户，返回 `{status: approved/rejected/missing}`（missing=无匹配待审批记录） | `DecideAppSessionToolApprovalResponse` |
| GET | `/api/app/{id}/access-point` | 查询访问点配置（内部管理视图，未保存过返回默认值；Admin+） | `AppAccessPointConfigResponse` |
| PUT | `/api/app/{id}/access-point` | 保存访问点配置（整体替换；仅外部应用，Admin+） | Empty |
| GET | `/api/external/app/{appId}/access-point` | 访问点**公开**配置（匿名，悬浮组件用；含 appName/avatarUrl/isAuth/enabled） | `ExternalAccessPointResponse` |
| POST | `/api/app/chat-attachment/extract` | 对话附件文本提取 `{objectKey, fileName}`（登录即可；objectKey 必须为 `public/chat/` 前缀防越权读私有文件；Maomi.ToMarkdown 进程内提取，超 12 万字符截断并标记） | `ExtractChatAttachmentResponse`（markdown/contentLength/truncated） |

**对话附件注入模型的两条路径（文档=文本、图片=多模态，2026-09-20 增补）**：文档附件由前端提取后内联在消息文本；图片附件由后端 `ChatAttachmentImageChatClient`（`IChatClient` 装饰器，`AppAgentFactory` 装配在内层 SDK 客户端之上、`UsageCapturingChatClient` 之下）在**请求发往模型前**把用户消息中的图片标记块重写为 `[图片附件：文件名]` 占位 + 追加 image/* `DataContent`（字节内联，OpenAI/Anthropic/Gemini 各协议适配器原生支持）。要点：①会话落库与历史回放仍存标记文本（持久化发生在 agent 层，装饰器只改发往模型的请求），新一轮历史重放走同一转换；②objectKey 解析优先标记块 `objectKey` 属性、历史消息回退从 URL `/static/` 后缀提取，且强制 `public/chat/` 前缀与无 `..`（与 extract 端点同约束，防越权读私有文件）；③svg 不内联（主流视觉接口不接受 image/svg+xml，保持链接文本）、读取失败/超 20MB 降级保留原标记文本不阻断对话；④同一次运行的工具循环轮次间按 objectKey 缓存字节。流程应用（WorkflowAppChatClient）不经过该装饰器。

> `modelId` 为 `ai_model.id`（uuid，可空）；传 null/空 Guid 表示不选择模型。`wikiIds` 为 `wiki.id`，`plugins` 为 `plugin.id`。

`QueryAppUserConfigCommandResponse` 字段新增：`toolApprovalMode`（auto/approval，无配置行默认 auto）与 `toolApprovalExemptNames`/`toolApprovalExemptPrefixes`（审批卡豁免工具清单，源自 `MoAI.AI.AppToolApprovalContract`，与 AI 模块闸口同源）。保存接口 `PUT /api/app/{id}/userconfig` 请求体新增可选 `toolApprovalMode`（null=不修改，非法值 400）。

`QueryAppAgentConfigCommandResponse` 字段：`appId / teamId / appType / prompt / modelId / wikiIds(long[]) / plugins(uuid[]) / skills(uuid[]) / executionSettings / openingStatement / openingStatementEnabled / quickInputs(string[]) / status / myRole`。

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

- **左侧菜单**：`config`（配置）/ `info`（信息，2026-09-19 增补，仅 Agent 应用，位于配置之后，成员可见）/ `logs`（日志）/ `monitor`（监控）；外部应用（`is_external=true`）额外 `access`（访问点）。采用与 `WikiDetail`/`TeamManage` 一致的 `Layout` + `Sider` + `Menu`，菜单项带图标（Setting/InfoCircle/Profile/AreaChart/Api）；`section` 非法或缺省回落 `config`。原 `AppManage.tsx` 单页左右分栏已删除，配置内容迁入 `AppConfigSection`。
- **分区可见性（前端渲染层）**：`logs`/`monitor` 仅团队 Owner/Admin 可见；`access` 仅 Owner/Admin 且外部应用可见；Member 只有 `config` 与 `info`（只读，无调试），深链 `/logs` 回落 `config`。
- **配置分区**（`AppConfigSection.tsx`）：左栏（`lg=15`）为 Agent 配置（含沙箱）；右栏（`lg=9`）为**调试对话** `AppDebugChat`（仅 Agent 应用且 Admin+；否则提示）。保存配置后**静默刷新**（`load(true)`），不卸载调试面板。基础信息（头像/名称/描述/授权与上架状态）自 2026-09-19 起拆至「信息」分区。
- **信息分区**（`AppInfoSection.tsx`，2026-09-19 增补）：应用基础信息维护（头像上传/类型标签/名称/描述/外部应用访问授权开关；内部应用为上架状态区块——公开、待审核可撤回、被驳回可重新申请，申请走 publication 模块 Modal）。保存信息/头像后 `onReload` 静默刷新；Member 表单只读且无保存入口。
- **调试会话（Redis 临时会话）**：`POST /api/app/{id}/debug/session`（Admin+）生成 `Guid.CreateVersion7()` 会话 id 并写 Redis 注册表 `appagent:debug:{id}`（TTL 2h 滑动）；对话仍走 `/api/agent/{appId}/chat`，`AppAgentDispatcher` 在无 `app_agent_session` 行时回落注册表（校验 `UserId`），以 `isDebug=true` 装配（跳过 `UsageCapturingChatClient`）；`AppChatFlushService.FlushAsync` 无 session 行即 return → 调试对话**不落库**。前端刷新即弃用会话 id。
- **日志分区**（`AppLogsSection.tsx`，Phase 2 已交付）：`GET /api/app/{id}/logs`（Admin+，分页，支持 标题关键字 / 用户类型 / 最后消息时间范围 过滤；数据源为全用户的正式会话 `app_agent_session`，即压缩后视图）+ `GET /api/app/{id}/logs/{sessionId}/messages`（Admin+，按 `seq` 返回该会话消息）。列表条目 `AppLogItem : AuditsInfo`，内部用户人名由 `IUserInfoFillService.FillAsync` 填充，外部用户按 `userType` + `ownerId` 展示（不填内部人名）。前端 `DataTable` + 详情 `Drawer`。
- **监控分区**（`AppMonitorSection.tsx`，Phase 3 已交付）：`GET /api/app/{id}/usage`（Admin+），返回用量汇总（调用次数 / 输入 / 输出 / 合计 token）与按模型分布。数据源为聚合表 `ai_model_token_audit`（`UseType=App` + `use_resource_id == appId` Guid + `team_id`），**最多滞后约 1 分钟**；调试会话不计数。本期**不含按日趋势**（聚合表无时间分桶）。
- **对话页顶栏（`AppChat.tsx`，2026-09-20 调整）**：仅保留 菜单（移动端）/ 返回 / 应用品牌（头像+名称+发布标签）/ 应用设置（用户级配置，专家选择并入其中，**无独立专家按钮**）；**不提供「管理」入口**（应用管理统一从团队「应用」分区卡片右上角进入，对话页聚焦对话本身）。
- **专家选择并入应用设置面板（`AppChat.tsx` + `chat/AppUserSettings.tsx`，2026-09-20 调整，原独立专家侧边栏移除）**：专家列表 = 本人个人提示词（`getMyPrompts`）+ 所在团队提示词（`getTeamPrompts`），含 个人/团队 来源标签。面板加宽至 380px，分区顺序为 专家 → 可用技能 → 工具审批；专家列表限高 300px 内滚。草稿以**当前生效专家**初始化（无会话=用户配置默认，有会话=会话 `promptId`）；保存时：无会话立即应用为当前专家（首轮 `createAppSession` 随会话一并创建），已有会话且专家变化则调 `PUT /app/session/{id}/prompt` 即时切换、再次点同一项保存取消（置 0），同时写入用户级配置作为新会话默认。选中专家仍在输入框上方提示条展示（可点 × 清除）；欢迎态热门专家胶囊点击即选用；切换会话按该会话 `promptId` 回显。
- **对话开场白（`AppConfigSection.tsx` + `AppChat.tsx`/`AppDebugChat.tsx`，2026-09-17 增补）**：配置分区在系统提示词下方提供「对话开场白」开关 + 内容 `TextArea`（≤4000，关闭开关保留内容），随 `PUT /agent-config` 一次提交；应用详情（`GET /app/{id}`，成员可读）下发 `openingStatement`/`openingStatementEnabled`。聊天页在**新会话态**（详情加载完成且未选历史会话）以助手气泡展示开场白：切换历史会话按服务端历史渲染（不含开场白），删除当前会话回到新会话态时重新补展示；调试面板在挂载与「清空」后同样展示。开场白是**前端本地展示消息**（固定 id），不参与模型上下文、不入会话历史。
- **快捷输入（`AppConfigSection.tsx` + `AppChat.tsx`，2026-09-20 增补）**：配置分区在开场白下方提供快捷输入列表编辑（增删改，≤10 条、单条 ≤200 字，保存时过滤空白项）；`PUT /agent-config` 携带 `quickInputs`（null=保持原值，兼容旧前端）。应用详情下发 `quickInputs`，聊天页欢迎态在输入卡下方以胶囊展示（未配置不渲染该区），**点击即直接发送**（`send(text)` 支持覆盖入参，首轮创建会话）；同时输入卡文本域默认三行（`minRows:3/maxRows:10`），移除欢迎态「输入问题开始对话」副标题（`landingSubtitle` 键删除，zh/en 同步）。已发布应用按发布快照下发（同开场白双轨，D42）。行为见 [@AP-S57](./bdd.md#ap-s57)/[@AP-S58](./bdd.md#ap-s58)。
- **重新发布入口（`AppWorkspace.tsx` + `AppConfigSection.tsx`，2026-09-20 增补）**：发布快照双轨（D42）的前端闭环——配置状态由配置分区加载/保存时上报工作台（`configStatus`/`onConfigStatusChange`，工作台为真值、分区回退本地值），已发布且状态=0 时：工作台头部出现「重新发布」主按钮（Popconfirm 确认，「取消发布」降为次按钮），配置区「已有未发布的配置修改」警告条附带「重新发布」action；确认后调用 `POST /app/{id}/publish`（发布接口本身即重发语义）把草稿写入快照，线上对话立即生效，入口与警告条消失。行为见 [@AP-S59](./bdd.md#ap-s59)。
- **对话输入卡与附件（`AppChat.tsx` + `chat/attachment.ts`，2026-09-20 增补；图片块格式同日随多模态注入调整；chip 展示同日加缩略图/类型图标）**：输入卡为两行式（文本域 + 底部工具行：左侧回形针附件按钮与审批模式标识、右侧深色方块上箭头发送/停止按钮）；开场白引导行在欢迎态为无边框纯文本。附件链路：`pre_upload_chat_file` 直传（`public/chat/{sha256}.{ext}`，白名单=文档+图片、≤20MB、一次最多 5 个）→ 文档调 `chat-attachment/extract` 提取 → chip 展示状态（上传中/提取中/就绪大小/失败可移除）；**chip 视觉**——就绪图片附件展示 32px 圆角缩略图（`getAttachmentFileIcon` 兜底：上传中/失败无地址回退图片图标），文档附件按扩展名展示类型图标（Word/Excel/PPT/PDF/Markdown/代码/通用文本，`attachment.ts` 的 `getAttachmentFileIcon` 映射），用户气泡 chip 同规则（图片 22px 缩略图 + `attachmentImageSrc` 兼容历史 `[图片附件](url)` 格式）；发送时拼 `buildOutgoingText`（用户输入 + `<moai-attachment name="…">提取内容</moai-attachment>` 标记块；图片块为 `<moai-attachment name="…" objectKey="…">裸下载地址</moai-attachment>`——objectKey 供后端读取字节做多模态注入、裸地址使气泡 chip 可点击打开，见 §4 与 @AP-S64），未就绪附件阻止发送；用户气泡经 `parseAttachmentMessage` 把标记块解析回附件 chip（正则兼容无 objectKey 属性的历史块；文档可展开查看提取文本、图片链接可打开），历史回看与服务端存储一致。
- **流程应用共用对话页的裁剪（`AppChat.tsx`，2026-09-20 增补）**：流程应用发布后同样经 `AppChat` 对话，但按应用详情 `appType=workflow` 裁剪 Agent 专属能力——应用设置面板与入口、专家（当前/热门）、技能勾选、工具审批模式不展示且不发起对应请求（流程对话后端不装配这些能力）；会话/开场白/快捷输入/附件照常。详见 [../app-workflow/sdd.md](../app-workflow/sdd.md) D36（行为 [@WF-S42](../app-workflow/bdd.md#wf-s42)~[@WF-S44](../app-workflow/bdd.md#wf-s44)）。
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
- **D40 对话开场白为前端展示消息，随应用详情下发**：开场白（启用开关 + 内容）存 `app_agent_config`，语义是「新会话开始时的第一屏引导」——**不参与模型上下文、不入会话历史**（区别于系统提示词，也不经对话端点下发）。挂载点选 `GET /app/{id}` 应用详情而非 `/agent-config`：聊天页用户多为 Member，详情本就成员可读，避免为取开场白抬高权限门槛。开关与内容分离：关闭开关保留内容，重新打开无需重写；`enabled && 内容非空` 才生效。
- **D41 沙箱资源上限强校验（2026-09-19）**：`execution_settings.sandbox` 的存活时间 / CPU / 内存不得超出超级管理员在系统设置配置的上限（见 [../settings/sdd.md](../settings/sdd.md) 决策 9）。校验在 `SaveAppAgentConfigCommandHandler` 经 `SandboxSettingsLimitValidator`（纯函数，`App.Core/Validation/`）执行：**仅本次保存启用沙箱时强校验**——未启用仅暂存不生效，避免 root 收紧上限后连带阻断其他字段的保存；CPU/内存按 K8s 数量解析比较（解析器 `SandboxQuantity` 在 Settings.Shared，前后端语义一致）。上限对配置页经 `GET /app/sandbox-limits` 下发（登录可读，非敏感值；`/api/settings` 为 admin 专属不能复用），前端 InputNumber max 与保存前拦截为体验层，后端 400 为最终防线。行为见 [@AP-S48](./bdd.md#ap-s48)。
- **D42 发布配置快照双轨（2026-09-20）**：镜像 `app_workflow_config` 的 `PublishedDefinition` 模式——`app_agent_config` 现有列即「草稿」，新增 `published_config`（发布时整行快照 JSON，契约 `AppAgentConfigSnapshot` 于 Database.Shared，供 App.Core/App.Workflow.Core/AI.Core 三层共用）与 `status`（0=草稿有未发布变更）。**发布**（`PublishAppCommandHandler`，行不存在以默认值建行）写快照并置 status=1；**保存**（含流程应用开场白 upsert）只改草稿并置 status=0；**取消发布**保留快照（与流程保留 `PublishedDefinition` 一致），运行时按 `publish_status` 门控。**读取**：`AppAgentFactory` 对正式会话（非调试）解析快照克隆实体（模型/提示词/插件/技能/执行参数与 `context.Config` 下游全走快照，调试会话与未发布应用走草稿；快照为 null 的存量已发布应用回退草稿行）；`QueryAppCommandHandler` 详情开场白、`AppChatFlushService` 落库压缩同规则；`agent-config` 查询响应新增 `status` 供前端提示「重新发布后生效」。流程应用开场白（D18 复用本表）随 `PublishAppWorkflowCommandHandler` 一并快照，`workflow/config` 的 `status` 合成「编排定义 ∧ 开场白」。行为见 [@AP-S54](./bdd.md#ap-s54)。
- **D43 快捷输入为管理员配置的展示型数据（2026-09-20）**：存 `app_agent_config.quick_inputs`（JSON 字符串数组，与 wiki_ids/plugins 同模式），**不参与模型上下文**，仅聊天页欢迎态展示与点击发送；随 `GET /app/{id}` 应用详情下发（复用 D40 开场白的挂载点与发布快照双轨，D42 契约 `AppAgentConfigSnapshot` 增 `quickInputs` 字段）。上限 10 条 × 200 字在命令 Validate 与前端双侧校验；保存语义对齐 `skills`：请求携带才覆盖（null=保持原值），空数组清空，入库前规范化（去空白/丢空串/去重）。流程应用不开放编辑（设计器系统设置仅维护开场白）。行为见 [@AP-S57](./bdd.md#ap-s57)。
- **D44 流程应用绑定为工具（2026-09-20）**：Agent 应用可把本团队**已发布**流程应用绑定为工具（`app_agent_config.workflow_apps`，保存时强校验团队/类型/发布状态；null=保持原值、空数组清空，随 D42 快照整行发布）。运行期由 `WorkflowAppToolProvider`（AI.Core/Tools，Order=11）装配：每个绑定的流程应用产出一个 `workflow__<应用名>` 工具（同名加短 id 后缀），参数契约 `{"query": "..."}`（兼容 question/input/text/prompt 与纯 JSON 字符串），调用经 `IWorkflowAppChatInvoker` **按发布快照**执行一轮流程，结束节点输出以 `{success, reply, instanceId}` 回传（中文不转义）；会话 id 透传使流程内 `sys.conversationId/sys.history` 与当前对话一致。**关键约束**：`IWorkflowAppChatInvoker` 在本提供者必须经 `IServiceProvider` 惰性解析——其实现构造链（WorkflowEngine → INodeExecutorRegistry → AiChatNodeExecutor → 宿主 IAiChatClient → AppContextProviderFactory）回到 `IEnumerable<IAppToolProvider>` 本身，构造注入形成 DI 环导致解析死锁（E2E AP-59 实测）。流程应用被取消发布/禁用后工具自然下线；审批模式下 workflow 属重要工具需人工批准。行为见 [@AP-S61](./bdd.md#ap-s61)/[@AP-S62](./bdd.md#ap-s62)/[@AP-S63](./bdd.md#ap-s63)。

### 工具人工审批（D-工具审批）

- 契约与键格式集中于 `MoAI.AI.AppToolApprovalContract`（AI.Shared，App.Core 引用之，避免环形依赖）：请求头 `X-Moai-Tool-Approval`（auto/approval，未携带按 auto）、Redis 键 `appagent:toolapproval:{id}`（记录）与 `appagent:toolapproval:pending:{sessionId}`（hash: field=审批 id, value=工具名，供决策接口按会话+工具名定位）。
- 闸口位置在 `AppToolContextProvider.InvokeJsonAsync`（call_tool 唯一同步调用点）：审批模式且工具 Kind ∈ {sandbox, dynamic, static, mcp, openapi} 时先 `AppToolApprovalService.WaitDecisionAsync`（400ms 轮询，最长 300 秒）再执行；拒绝/超时向模型返回说明性错误 JSON，对话流不断。
- 审批模式为**用户级**偏好（`app_user_config.tool_approval_mode`），随每轮对话 SSE 请求头即时下发（改模式无需重启/下一轮即生效）；外部渠道/飞书等无请求头场景恒为自动模式。
- 前端审批卡由 AG-UI `TOOL_CALL_END` 事件解析 call_tool 内层 `toolName/argumentsJson` 驱动；豁免清单由 userconfig 下发（`search_knowledge_base` 与 `skill_*` 前缀），保证前后端判定同源。
- 审批等待期间 SSE 连接保持（无字节输出），Kestrel 无空闲响应超时；反向代理场景需放宽读超时（>300 秒）。

### 审批策略（D45 插件白名单与沙箱自动放行）

- **D45 审批策略（2026-09-20）**：审批模式下的**应用级**放行白名单，存 `execution_settings.toolApproval` 节（`{ autoApprovePlugins: ["<pluginId>"], sandboxAutoApproved: bool }`，与沙箱配置同载体）——免加列、随 D42 快照整行发布（发布后改草稿不影响线上放行）。契约 `MoAI.AI.AppToolApprovalPolicy`（AI.Shared，闸口/保存校验/userconfig 三方共用）。
  - 判定：`AppTool` 新增 `SourceId`（插件/流程应用 id，由 Plugin/Workflow 提供者填充），闸口在 Kind 命中重要工具后再查策略——沙箱开关命中 `kind=sandbox`，或 `SourceId` 在插件白名单内 → 直接执行，不建待审批记录；其余工具仍按 D-工具审批挂起。用户切回自动模式则策略无差别全放行（既有语义）。
  - 保存校验：白名单必须是本次绑定插件的子集（未绑定/非法 id/结构不合法一律 400）；前端保存前先收敛（解绑插件自动剔出白名单）。
  - 下发：userconfig 按发布快照解析策略并展开为**工具名**清单（静态/动态=插件名，MCP/OpenAPI=`{插件名}__{函数名}`，拼装契约 `AppPluginToolNaming`）与沙箱前缀 `sandbox_`，前端据此免展示审批卡（与豁免清单同一判定函数）。
  - 行为见 [@AP-S65](./bdd.md#ap-s65)~[@AP-S68](./bdd.md#ap-s68)。

### 外部应用能力限制（D46 沙箱与技能强制关闭）

- **D46 外部应用不能绑定技能也不能开启沙箱（2026-09-21）**：外部应用面向外部用户/匿名开放，技能包文件与代码执行面不可外溢，双层拦截：
  - 保存侧 `SaveAppAgentConfigCommandHandler`：`app.IsExternal` 时显式携带非空技能或执行参数中 `sandbox.enabled=true` 一律 400（判定复用 `SandboxSettingsLimitValidator.IsSandboxEnabled`）；保存同时把 `Skills` 收敛为 `[]`，历史存量随保存清空。
  - 装配侧 `AppAgentFactory`：对话装配对外部应用**克隆生效配置**（`ResolveEffectiveConfig` 结果可能是变更跟踪中的草稿行，禁止就地修改）——`Skills="[]"`、`Sandbox=null` 后回写 `ExecutionSettings`；沙箱/技能/审批策略工具链均按配置解析，一处生效覆盖发布快照与存量草稿、内部工作台/外部端点/调试全部对话入口。
  - 前端：外部应用配置分区隐藏「默认技能」「沙箱参数」与审批策略沙箱开关并加提示，保存固定 `skills: []`、`sandbox.enabled=false`。
  - 流程应用绑定为工具本就拒绝外部应用（`!IsExternal`），不受本决策影响。行为见 [@EA-S13](./bdd.md#ea-s13)、[@EA-S14](./bdd.md#ea-s14)。

## 7. 已知问题 / 下阶段

- **外部应用的实际访问链路未实现**：`is_external`/`is_auth` 已落库并可管理，但「应用接入 key」「外部用户 token」「`/external` 对话端点」分别为设计文档的第 2、3 期，尚未交付。当前外部应用只能被创建/配置/发布，不能真正被外部调用。
- 新建弹窗取消或创建失败时，已直传的头像对象会成为**孤儿文件**（无引用、无清理）。
- **库列注释与代码语义不一致**：`app.app_type` 的库注释仍是旧的「普通应用=0,流程编排=1」，而代码/文档已按 `AppType`（Agent=0 / 流程=1）执行。由于 `AppEntity` / `AppConfiguration` 由脚手架**按库注释逆向生成**，只改 C# 注释会在下次重跑时被覆盖——要彻底一致需先改库列注释（`comment on column app.app_type is ...`）再重跑脚手架。
- 应用删除、启用/禁用、分类绑定、列表分页与关键字筛选未实现；当前列表为团队维度全量。
- **「使用」应用尚无动作**：Member 在应用分区只能看到只读卡片——打开应用/对话依赖 Agent 运行时（会话），属下阶段；当前不放置无效的「打开」按钮。
- 应用列表未展示创建人（未接入 `IUserInfoFillService`）。
- **Agent 应用配置已可读写，但尚未运行**：`app_agent_config` 的 `model_id`/`prompt`/`wiki_ids`/`plugins` 已有读写接口与管理页；`execution_settings` 的 `sandbox` 子对象可经配置页读写（启用沙箱时受系统上限强校验，见 D41），其余键（temperature/topP/maxTokens 等）新建配置行时落 `{}`、默认值由运行时决定，会话（`app_agent_session`/`app_agent_message`）与对话运行未实现。**未选模型是合法状态**（空 Guid），「不指定模型时用什么」留待会话运行阶段定义。
- **绑定无反向查询**：`wiki_ids`/`plugins` 为 JSON 文本，删除插件/知识库时不会级联清理引用（D10 的代价）；已绑定的资源被删除后，管理页会显示一个无法解析为选项的 id（表现为裸 id 标签）。**模型同理**：模型被停用/取消授权后，已保存的 `model_id` 不会自动清空，管理页会显示空标签（选项已不在列表里）。
- 流程应用的应用级配置（`app_workflow_design` 等）未实现；管理页对流程应用只开放基础信息。
- 消息表未落 `status`（生成中/完成/失败）与逐条 token；会话表已按会话维度累计 token。
- `app` 表未建 partial 唯一索引，并发创建同名应用存在极小概率穿透（Handler 先查后写）。
- **访问点组件浏览器走查已完成**（2026-09-21）：宿主页引入嵌入代码后悬浮按钮渲染、点击开面板、匿名换 token、建会话、AG-UI 流式回复全链路通过。嵌入代码 `src` 指向站点自身源（开发期由 Vite 中间件挂 `/embed/moai-widget.js` 并代理 `/api`；产物仍由 `npm run build:embed` 输出后端 wwwroot，分离部署用 `data-server` 覆盖）；widget 初始化失败/访问点未启用改为 console 告警不再静默，`crypto.randomUUID` 在非安全上下文降级。
- **悬浮组件跨源宿主页兼容**（2026-09-21 补）：`file://`/沙箱等 `Origin: null` 宿主页的预检由后端 CORS `AllowAnyOrigin` 统一回答，开发期必须保持 Vite `server.cors: false`（内置 cors 只放行 localhost 系 origin，会抢答预检且不带 ACAO，报「No ACAO」的伪跨域错误）；代理对后端不可达回 502 `backend_unreachable` 并带 ACAO，避免误报成跨域；后端对 PNA（Private Network Access，公网页面→本机服务）预检回 `Access-Control-Allow-Private-Network: true`（Program.cs，置于 UseCors 之前）；widget 全部 `crypto.randomUUID` 调用走降级（`about:blank`/http 局域网 IP 宿主页为非安全上下文，直接调用必 TypeError）。
- **外部对话端点的会话范围守卫在中间件**：`ExternalAuthenticationMiddleware` 对 `/api/external/agent/{appId}/chat` 校验 `appId ∈ token 授权范围` + 应用可用（已发布、未禁用）；对话中未配模型时派发器以 SSE 文本返回装配错误（HTTP 200），不计费。真实模型对话的端到端 E2E（需自建桩模型与渠道配置）随访问点 4c 交付。
- **外部 access token 无即时吊销**：吊销接入/换绑应用依赖 refresh（≤7d）或 access 自然过期（2h）；期间已签发 access token 仍有效（无状态 JWT 取舍）。
- **监控无按日趋势**：用量数据来自聚合表 `ai_model_token_audit`，无时间分桶，无法画日趋势；趋势需逐次用量日志或按日聚合表，留后续迭代。
- **监控最多滞后约 1 分钟**：应用对话只累加 Redis 计数器，由 Hangfire 每分钟 flush 到 `ai_model_token_audit`。
- **日志为压缩后视图**：`app_agent_message` 只保留压缩后视图，被压缩掉的历史原文不可回溯；调试会话不落库，故不出现在日志中。
- **日志仅 Admin+**：Member 查看返回 403、非成员 404；`AuditsInfo.CreateUserId` 为 `int`，外部用户 `long` id 仅作 `ownerId` 原样返回（非内部人名）。
- **调试会话残留热态**：调试会话在 Redis 的消息/快照沿用 24h TTL，注册表 2h 过期后不可再解析，键随 TTL 自然清理；不做服务端即时销毁。
- **调试会话归属仅按 UserId 校验**：注册表未存 `UserType`，与正式会话的归属校验等价；如后续需唯一用户类型可扩展。
- **开场白仅内部 Web 聊天页与调试面板消费**：外部悬浮组件（widget）与飞书入口尚未读取 `openingStatement`，如需一致体验需在各入口分别接入；开场白不进模型上下文，改配即时生效（下一个新会话即可见）。
