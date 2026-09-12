# 应用管理模块设计规格（SDD）

> 关联：[SDD](./sdd.md) ｜ [BDD](./bdd.md) ｜ [TDD](./tdd.md) ｜ [SOP](./sop.md) ｜ 上游：[../team/sdd.md](../team/sdd.md) ｜ 证据：[local-dev/app-e2e.mjs](../../local-dev/app-e2e.mjs)

- 日期：2026-09-10（2026-09-11 增补：创建/编辑支持头像与「允许外部使用」开关；2026-09-11 增补：应用改卡片展示 + 应用管理页可配置插件/知识库/提示词；2026-09-11 增补：管理页改**单页左右分栏**并支持**对话模型**选择）
- 状态：数据库 + 后端 API + 前端团队内页面（应用卡片列表 + 应用管理页）已实现**并全链路验证**；**发布**（`publish_status`/`publish_time`）与会话 CRUD 已实现；Agent 应用的**会话运行**（对话/上下文/知识库 RAG）见 [../ai/sdd.md](../ai/sdd.md)；外部用户使用仍为下阶段
- 领域：`src/app`（Shared/Core/Api），前端 `ui/src/pages/teams/apps`（团队页「应用」分区 + 应用管理页）
- Schema 真源：库表现状 + `src/database/MoAI.Database.Postgres/Data/App*.cs`（脚手架逆向生成）；原 `asserts/app.sql` / 库表 `app_agent_*` 已随仓库 DDL 清理移除

## 1. 目标

应用是**团队下的产物**，入口也只存在于**团队之内**：进入「团队」→ 某个团队 → 「应用」分区，**团队管理员（Owner/Admin）**在此创建应用并指定应用类型，随后管理/配置应用；**普通成员（Member）进入团队只能使用**——可见分区仅 信息 / 应用 / 知识库，看不到成员、网关、插件、变量、设置这些管理分区，也进不到应用配置。本期交付：应用管理与基础信息（名称、描述、头像、「允许外部使用」）、**应用卡片列表**，以及 **Agent 应用管理页**（左栏基础信息，右栏对话模型、系统提示词、允许使用的插件与知识库）；Agent 应用的会话运行与流程应用的差异化配置留待后续。

> 侧边栏**不再有**一级「应用」菜单（`nav.app`、`/app` 路由与 `ui/src/pages/apps` 页面均已移除）：跨团队的应用聚合页把「管理」放到了团队之外，与「应用属于团队」相悖。

## 2. 数据模型

- `app`：`id(uuid) / name(20) / description(255) / team_id(int) / is_public / is_disable / classify_id / is_foreign / is_auth / app_type / avatar(255) / ` 审计四件 + `is_deleted(bigint)`。
- **应用类型** `app_type`：`0=Agent 应用`、`1=流程应用`（`MoAI.Database.Enums.AppType`，成员带 `JsonPropertyName("agent"/"workflow")`，接口出参为字符串）。
- **「允许外部使用」开关写 `enable_foreign`**（列注释即「允许外部使用」，见 D16）：创建与更新均可设置，列表/详情回读；**开关只落库**——团队外用户「使用应用」的能力本身未实现（无对外入口、无外部用户鉴权）。
- `is_public`（公开到团队外使用）本期**未使用**（保持落库默认 false）；legacy 的 `is_foreign` / `is_auth` 两列已随库表调整移除（现为 `enable_foreign`）。
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
| `user_type` | int | `MoAI.Infra.Models.UserType`：0=识别不到 1=外部用户 2=外部应用 3=内部普通用户 |
| `input_tokens` / `out_tokens` / `total_tokens` | int | 会话 token 累计 |
| `last_message_time` | timestamptz | 最后消息时间，**会话列表按此倒序** |
| `create_user_id` | bigint | 会话归属用户 |
| 审计四件 + `is_deleted` | | bigint 软删除 |

索引：`(app_id)`、`(team_id)`、`(app_id, create_user_id, last_message_time DESC)`。

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

## 3. 角色与权限矩阵（Handler 层判定，依赖 team_user 事实）

| 操作 | Owner/Admin | Member | 非成员 |
|---|---|---|---|
| 列表 / 详情 | ✅ | ✅ | 404（不泄露存在性） |
| 创建（含指定应用类型、头像、允许外部使用） | ✅ | 403 | 404 |
| 更新基础信息（名称/描述/允许外部使用） | ✅ | 403 | 404 |
| 设置头像（独立接口，编辑时即时生效） | ✅ | 403 | 404 |
| 查询 Agent 应用配置 | ✅ | ✅ | 404 |
| 保存 Agent 应用配置（插件/知识库/提示词） | ✅ | 403 | 404 |

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
| POST | `/api/app` | 创建应用 `{teamId, name, description?, appType, avatar?, enableForeign?}` | `SimpleGuid`（应用 id） |
| GET | `/api/app/list?teamId=` | 团队应用列表（含 myRole、enableForeign） | `QueryAppsCommandResponse` |
| GET | `/api/app/{id}` | 应用详情 | `QueryAppCommandResponse` |
| PUT | `/api/app/{id}` | 更新基础信息 `{name, description?, enableForeign?}`（应用类型不可改） | Empty |
| POST | `/api/app/{id}/avatar` | 设置头像 `{objectKey}`（须为已登记上传文件；编辑态使用） | Empty |
| GET | `/api/app/{id}/agent-config` | 查询 Agent 应用配置（未保存过时返回空配置，不 404） | `QueryAppAgentConfigCommandResponse` |
| PUT | `/api/app/{id}/agent-config` | 保存 Agent 应用配置 `{modelId?, prompt, wikiIds[], plugins[]}` | Empty |

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
- 分区菜单顺序：信息 / **应用** / 成员 / 模型网关 / 知识库 / 插件 / 环境变量 / 设置；默认落在「信息」。
- **角色可见性（前端渲染层）**：
  - Owner/Admin：全部分区可见。
  - Member：只保留 信息 / 应用 / 知识库；**成员、模型网关、插件、环境变量、设置被隐藏**，直接改 URL 访问也会回落到「信息」。
  - 该收敛只影响渲染，真正的门禁仍在后端 Handler（Member 写操作 403、非成员 404）。
- 分区内容（收窄为**单团队**）：**卡片网格**（`Row`/`Col`，与 `/wiki` 卡片同构）——卡片含 头像 + 名称 + 类型标签，描述（2 行省略），底部一行 创建时间 + 「外部使用」状态标签（已开启/未开启，`enableForeign`）；**卡片右上角「管理」**（仅 `canManage` 渲染）进入应用管理页。列表顶部左侧为说明文案（管理员为「卡片右上角管理」提示，Member 为只读说明），右侧为「新建应用」。
- 新建弹窗：应用类型 + **头像** + 名称 + 描述 + **「允许外部使用」开关**；**团队由所在分区确定，不再选团队**。
  - 头像「先直传、后提交」：选图即走 `uploadImageWithKey` 拿 `objectKey` 并在弹窗内预览，点确定时随创建请求一起提交。取消/关闭弹窗会丢弃已选头像，已上传的对象成为孤儿文件（与仓库其他「先传后用」场景一致，暂不做清理）。
- Member 视图：只读卡片，顶部一行说明「只能查看与使用应用；创建与配置需要团队管理员」，不渲染新建按钮与「管理」入口。
- 文案走 `t()`，zh-CN / en-US 同步（`appManage.*`、`team.apps`）。
- 头像经 `resolveStorageUrl` 转可访问地址（同团队/知识库头像）。

### 5.1 应用管理页（`ui/src/pages/teams/apps/AppManage.tsx`）

- **路由**：`/team/:teamId/app/:appId`（**无分区参数**）；页面为**单页左右分栏**（`Page` 面包屑 + `Row`/`Col`），**左侧「应用信息」、右侧「Agent 配置」**，不做左侧菜单/分区切换。
- **左栏 应用信息**（`DSCard` 标题「应用信息」）：头像（选中即上传生效，走 `POST /{id}/avatar`）+ 类型（只读标签）+ 名称 + 描述 + 「允许外部使用」开关 + 「保存信息」（`PUT /{id}`）。即原编辑弹窗改为页内表单，`TeamApps` 不再有编辑入口。
- **右栏 Agent 配置**（`DSCard` 标题「Agent 配置」）：对话模型 + 提示词 + 允许使用的插件 + 允许使用的知识库 + 「保存配置」（`PUT /{id}/agent-config`）。四个字段**同一份配置状态、同一个保存按钮**，一次提交完整配置。
  - **对话模型**：`Select`（单选、可清空），选项来自 **`GET /api/team/{id}/gateway/models`**（团队可用网关模型：公开模型 + 已授权本团队的私有模型）——与团队「模型网关」分区同源，**取值范围天然等于「该团队有权使用」**。空值表示不指定模型（落空 Guid）。
  - **插件 / 知识库**：`Select mode="multiple"`，选项来自 **`GET /api/team/{id}/plugin/list`**（团队可访问插件）与 **`GET /api/wiki/list?teamId=`**（本团队知识库）；`PluginId` 为空 Guid 的内存静态插件（无 DB 记录）在展示层被过滤，不可绑定。
  - **提示词**：`Input.TextArea`（maxLength 4000 + `showCount`）。
- **流程应用**：右栏**不渲染配置项**，只展示「流程应用的配置能力尚未开放」提示；左栏仍是完整基础信息。
- **Member 访问**：页面渲染为只读（表单 `disabled`、无保存按钮）并顶部提示；后端仍以 403 兜底。

## 6. 关键决策

- **D1 应用类型语义**：`0=Agent 应用`、`1=流程应用`；同步修正 `app.app_type`、`AppEntity`、`AppConfiguration` 的旧注释（原为「普通应用/流程编排」）。
- **D2 创建权归团队管理员**：应用是团队下的产物，与知识库/插件一致按团队角色门禁；Member 只读。
- **D3（修订）本期交付基础信息 + 头像 + 「允许外部使用」开关**：名称/描述/头像与 `enable_foreign` 开关在**创建与编辑**时都可设置。**仍未开放**：启用/禁用（`is_disable`）、分类绑定（`classify_id`）、`is_public`、删除、列表分页与筛选。
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
- **D16（外部开关落哪一列）写 `enable_foreign`，不用 `is_public`**：`app` 表有两个都带「外部」语义的列——`is_public`（公开到团队外使用）、`enable_foreign`（允许外部使用）。本轮的「允许外部使用」与 `enable_foreign` 的列注释字面一致，因此写它；`is_public` 保持落库默认 false、不暴露设置入口。**开关只保存状态**，团队外用户使用应用的入口与鉴权未实现。
- **D17（列表改卡片 + 管理页收口）**：应用列表由 `DataTable` 改为**卡片网格**，卡片右上角「管理」进入独立管理页；原编辑弹窗的职责（基础信息）并入管理页，`TeamApps` 只保留创建。理由：卡片更适合应用这类带图标的对象，且把「基础信息 + 资源配置」收口到一处，避免列表页同时承载编辑与配置两条入口。
- **D18（配置读权限给成员）**：`GET /agent-config` 允许团队 **Member** 读（配置属于「使用应用」的一部分，与列表/详情同级），写仍限 Admin+。若后续需要隐藏提示词，再按角色裁剪响应字段即可，不改接口形状。
- **D19（绑定校验在应用层，越权 400）**：`wiki_ids`/`plugins` 取值为「该团队有权使用」的资源，校验放在 `SaveAppAgentConfigCommandHandler`（wiki 按 `team_id`、plugin 按团队自有或系统公开/已授权），**不建物理外键**（沿用 D10）；内容越权返回 **400**（区别于角色的 403），且校验先于写入，失败不落库。
- **D20（配置保存为整体替换）**：管理页所有配置项共用一个 `PUT /agent-config`，提交时携带完整 `{modelId, prompt, wikiIds, plugins}`。不做「按字段局部更新」的接口，避免并发/漏字段导致绑定被静默清空。
- **D21（管理页不做左侧菜单，单页左右分栏）**：初版管理页照 `WikiDetail` 用「左 `Menu` 分区 + 右 `Content`」，把基础信息/插件/知识库/提示词拆成四个分区。实际使用中配置项只有四项、且互相有关联（模型 + 提示词 + 插件 + 知识库共同定义一次对话），分区切换反而要来回跳，于是**去掉左侧菜单**，改为**一页左右两栏**：左栏应用信息、右栏 Agent 配置。路由随之简化为 `/team/:teamId/app/:appId`（去掉 `:section?`），页面内不再有 URL 级的子状态。
- **D22（对话模型取值范围 = 团队可用网关模型）**：`modelId` 写 `ai_model.id`（D9），取值来源**复用团队「模型网关」的既有查询** `GET /api/team/{id}/gateway/models`（公开模型 + `ai_model_authorization` 已授权本团队的私有模型，且模型与渠道均启用），不在应用模块另造一份模型可见性逻辑。服务端同样按这套口径复核（`SaveAppAgentConfigCommandHandler.ValidateModelIdAsync`）：模型不存在/未启用/渠道停用 → 400，私有模型未授权本团队 → 400。**未选模型允许**（落空 Guid），此时是否回落到默认模型属于会话运行阶段的事。

## 7. 已知问题 / 下阶段

- 「允许外部使用」**只保存开关状态**：团队外用户的入口、外部会话与鉴权均未实现（`app_agent_session.user_type` 已为其预留）。`is_public` 未使用。
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
