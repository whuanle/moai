# 应用工作台 · 调试会话 · 日志与监控设计

- 日期：2026-09-14
- 状态：待评审
- 关联：[应用 SDD](../../app/sdd.md) ｜ [应用 BDD](../../app/bdd.md) ｜ [CQRS 规范](../../cqrs-conventions.md) ｜ [前端规范](../../../ui/docs/frontend-conventions.md) ｜ [接口真源](../../api_interface.md) ｜ 前置设计：[外部应用 · 应用接入](../specs/2026-09-13-external-app-and-access-design.md)

## 背景与目标

应用管理页当前是**单页左右分栏**（左「应用信息」、右「Agent 配置」），且应用要**发布后**才能对话调试。参考 Dify 的编排体验，本期把管理页升级为**应用工作台**：

1. 按应用类型提供分区导航：**内部应用** = 配置 / 日志 / 监控；**外部应用**额外多一个 **访问点**。
2. 配置分区改为**左配置、右调试对话**，未发布即可用**临时配置调试**。
3. 调试对话**不落库**，只放 Redis，前端刷新即弃用。
4. 补齐「日志」（对话日志）与「监控」（用量统计）两个只读看板。

## 范围

**包含：**

1. 应用工作台外壳与路由（`配置 / 日志 / 监控` + 外部应用 `访问点`）。
2. 配置分区：左栏配置表单（沿用现有字段）+ 右栏**临时调试对话**；调试会话 Redis 化、不落库。
3. 日志看板：按应用分页查询**全用户会话**与消息详情（Admin+）。
4. 监控看板：按应用统计调用次数与 token（汇总 + 按模型分布；本期不含按日趋势）。
5. 访问点分区：**占位**（展示后端地址 / 前端嵌入地址规划，功能后续迭代）。

**不包含（后续迭代）：**

- 访问点的实际能力：JS 悬浮对话组件、前端嵌入地址生成、密钥/域名绑定（见「后续迭代」）。
- 日志的**原始消息**留存（当前只留压缩后视图）、日志导出、标注/反馈。
- 监控的实时 QPS / 成功率 / 延迟（需新增运行期埋点）。
- 流程应用（`app_type=1`）的配置与调试（沿用现状只开放基础信息）。
- 调试配置本身临时化（本期调试始终读取**已保存**的配置）。

## 术语

| 名称 | 含义 |
|---|---|
| 工作台（App Workspace） | 应用管理页，含 配置/日志/监控(/访问点) 分区的容器 |
| 调试会话（Debug Session） | 仅存于 Redis、不落库、不计数的一次性对话会话；刷新即弃用 |
| 正式会话 | 落在 `app_agent_session`/`app_agent_message` 的持久会话 |
| 日志 | 正式会话的**压缩后视图**（`app_agent_message`）按应用 + 全用户查询 |
| 监控 | 基于聚合表 `ai_model_token_audit` 的应用用量统计（汇总 + 按模型分布） |

## 信息架构与路由

工作台路由：`/team/:teamId/app/:appId/:section?`，`section ∈ {config, logs, monitor, access}`，缺省 `config`，非法值回落 `config`。

- `/team/:teamId/app/:appId/chat`（沉浸式对话页 `AppChat`）**保留**；React Router 静态段优先，`chat` 不会被 `:section` 吞掉。
- 分区导航用**左侧菜单**（`配置 / 日志 / 监控`，外部应用追加 `访问点`），与 `WikiDetail`/`TeamManage` 的 `Layout` + `Sider` + `Menu` 范式一致；配置分区内部再左右分栏（左配置、右调试）。
- 访问点 Tab 仅当 `detail.isExternal === true` 渲染；内部应用不出现。
- 团队列表页（`TeamApps` / `TeamExternalApps`）不变，卡片「管理」进入工作台。

## 调试会话设计（方案 A：复用对话链路 + Redis 注册表）

### 目标

未发布应用也能调试；调试对话**不写任何业务表**、**不计入用量**；前端刷新后不可续接。

### 会话标识与存储

- 后端生成调试会话 id（`Guid.CreateVersion7()`），与正式会话同为 Guid，复用 AG-UI `threadId` 通道。
- Redis 注册表键：`appagent:debug:{sessionId:N}`，值 `DebugSessionRegistry{ AppId, TeamId, UserId, CreateTime }`，**TTL 2 小时（滑动，每轮解析成功时刷新）**。
- 消息/快照仍写现有热态键 `appagent:msg:*` / `appagent:session:*`（24h TTL）；调试注册表过期后该会话不可再解析，残留键随 TTL 自然清理。
- 无新增 DDL、无新增业务表。

### 后端接口

`POST /app/{id}/debug/session`（Admin+）

- 请求模型 `CreateDebugSessionCommand`：仅 `AppId`（来自路由）；继承 `IUserIdContext`。
- 校验链：应用存在（404）→ 非外部/外部均可（调试目的）→ `AppType==Agent`（否则 400）→ `!IsDisable`（否则 403）→ 团队角色 Admin+（Member 403、非成员 404）。
- 生成 `sessionId` 写注册表，返回 `SimpleGuid`。

### 运行时改动（`MoAI.AI`）

1. `AppAgentDispatcher.ResolveInnerAsync`：查 `app_agent_session` 为空时，回落查 `IDebugSessionRegistry.TryGetAsync(sessionId)`；命中且 `UserId == 当前用户` → 用注册表里的 `AppId/TeamId` 装配 Agent（`isDebug: true`）；未命中/非本人 → 404「会话不存在」。
2. `AppAgentFactory.CreateAsync(...)` 增 `bool isDebug = false`：调试时不包裹 `UsageCapturingChatClient`（不计数）。
3. **不落库天然成立**：`AppChatFlushService.FlushAsync` 查不到 `app_agent_session` 行即 `return`，Redis 消息不写 `app_agent_message`；`AppAgentSessionStore.SaveSessionAsync` 仍写 Redis 快照，`DeleteSessionAsync` 仅清 Redis。
4. `IDebugSessionRegistry` 为 `[InjectOnSingleton]`，基于现有 `IRedisDatabase`（键前缀 `moai:`）。

### 前端调试流

- 进入配置分区即调用 `POST /app/{id}/debug/session` 取 `sessionId`，存组件内 `useRef`（不持久化）；组件卸载/页面刷新即丢弃。
- 发送复用现有 `createAppChatAgent(appId, sessionId)` + `runAppChat`（AG-UI SSE）。
- 若后端返回会话不存在（注册表过期），前端**自动重建一次**调试会话并重试；仍失败则提示。
- 「清空对话」= 丢弃当前 id 并新建调试会话。
- 调试面板顶部标注「未发布也能调试 · 对话不保存」，并**不复用**会话列表侧栏（调试无历史）。

## 日志看板

### 数据源与限制

- 会话头 `app_agent_session`（全用户，`app_id = :id`）+ 消息 `app_agent_message`（压缩后视图）。
- **已知限制**：被压缩掉的历史原文已丢弃，日志展示的是压缩后视图（与正式对话所见一致）；不提供原始消息回溯。
- 外部身份的会话 `create_user_id` 指向 `external.id`，无法填充内部用户人名；按 `user_type` 区分展示（内部用户填人名，外部显示「外部用户 #id」）。

### 接口

| 方法 | 路由 | 说明 |
|---|---|---|
| GET | `/app/{id}/logs` | 分页会话日志，Admin+ |
| GET | `/app/{id}/logs/{sessionId}/messages` | 指定会话消息详情，Admin+ |

`QueryAppLogsCommand : PagedParamter, IUserIdContext`：`AppId`、`Keyword?`（标题）、`UserType?`、`From?`/`To?`（`DateTimeOffset`）、`PageNo/PageSize`。

- 校验：应用存在 + 团队角色 Admin+（Member 403、非成员 404）；`session.app_id == appId` 兜底。
- 排序：`LastMessageTime DESC`；返回 `Items + TotalCount`。
- 列表项 `AppLogItem : AuditsInfo`：`SessionId`、`Title`、`UserType`、`UserId`、`UserName`（`IUserInfoFillService.FillAsync` 填充）、`InputTokens/OutTokens/TotalTokens`、`LastMessageTime`、`CreateTime`。
- 消息详情 `AppLogMessageItem`：`Seq`、`Role`、`Content`、`Reasoning?`、`ToolCalls?`、`ToolCallId?`、`CreateTime`。
- 管理端查询**不校验会话归属**（日志是管理能力），但必须校验应用属于该团队。

## 监控看板

### 数据源

- 汇总与按模型分布：`ai_model_token_audit`（`UseType=App`、`use_resource_id = appId`，该列为 Guid，无需字符串化迁移；带 `team_id`）。
- 应用对话只累加 Redis 计数器，由 Hangfire 每分钟 flush 到聚合表，故监控**最多滞后约 1 分钟**；调试会话不计数，天然不进入监控。
- **本期不做按日趋势**：原设计假设 `ai_model_usage_log` 逐次日志，但应用对话从不写该表；聚合表逐维一行、无时间分桶，无法画日趋势（趋势需逐次用量日志或按日聚合表，留后续）。

### 接口

`GET /api/app/{id}/usage`（**无 `from/to/granularity`**）

- 校验同日志（Admin+；Member 403、非成员 404、应用不存在 404）。
- 出参 `QueryAppUsageCommandResponse`：
  - `Summary`：`CallCount`、`PromptTokens`、`CompletionTokens`、`TotalTokens`。
  - `ByModel`：`[{ ModelId, ModelName, CallCount, TotalTokens }]`（`ModelName` 缺失时回落 `ModelId`）。
- **不含 `Series`**（本期不做按日趋势）。

## 访问点（占位，后续迭代）

外部应用专属分区，本期只渲染**规划说明 + 只读地址预览**，不实现生成能力：

- 后端地址（规划）：`POST {Server}/external/agent/{appId}/chat`。
- 前端嵌入地址（规划）：`{Server}/embed/app/{appId}.js` + 悬浮对话组件挂载脚本。
- 授权方式：复用「应用接入」`access_app` 的 key（见前置设计 D2）。

后续迭代项：JS SDK 构建与产物托管、嵌入代码生成与复制、域名白名单、悬浮组件主题定制、访问点启停。

## 权限矩阵

| 操作 | Owner/Admin | Member | 非成员 |
|---|---|---|---|
| 进入工作台 · 配置（读） | ✅ | ✅（只读） | 404 |
| 配置保存 / 发布 | ✅ | 403 | 404 |
| 日志看板 | ✅ | 403 | 404 |
| 监控看板 | ✅ | 403 | 404 |
| 访问点（仅外部应用） | ✅ | 403 | 404 |
| 创建调试会话 / 调试对话 | ✅ | 403 | 404 |
| 正式会话对话（已发布 / 公开应用） | ✅ | ✅ | 公开应用 ✅ |

## 后端接口汇总

| 方法 | 路由 | 角色 | 说明 |
|---|---|---|---|
| POST | `/app/{id}/debug/session` | Admin+ | 创建 Redis 调试会话，返回 sessionId |
| GET | `/app/{id}/logs` | Admin+ | 分页对话日志 |
| GET | `/app/{id}/logs/{sessionId}/messages` | Admin+ | 日志消息详情 |
| GET | `/app/{id}/usage` | Admin+ | 用量统计 |

约定：请求模型实现 `IModelValidator<T>` 并写 `static Validate`；命令继承 `IUserIdContext`（Handler 禁注入 `IUserContextProvider`）；列表 DTO 继承 `AuditsInfo` 并用 `IUserInfoFillService.FillAsync`；时间用 `DateTimeOffset`；分页用 `PagedParamter`；`BusinessException` 显式设 `StatusCode`；DI 用 Maomi 特性。Controller 仅转发，角色门禁在 Controller/端点、目标保护在 Handler。

## 前端设计

- 新增工作台外壳 `AppWorkspace.tsx`（路由容器 + 左侧菜单），拆分：
  - `AppConfigSection`（左配置表单 + 右 `AppDebugChat`）
  - `AppLogsSection`（`DataTable` + 详情 `Drawer`）
  - `AppMonitorSection`（`Statistic` 汇总 + 按模型 `DataTable` + 占比；不引入图表库）
  - `AppAccessSection`（占位）
- `AppManage.tsx` 现有左右分栏内容迁入 `AppConfigSection`；迁移完成后删除 `AppManage.tsx`，路由直接挂 `AppWorkspace`。
- 抽取对话渲染为**展示型组件** `ChatConversation`（消息流 + 输入框 + 运行控制），供 `AppChat` 与 `AppDebugChat` 复用；`AppDebugChat` 只替换会话来源与侧栏（无历史列表）。
- 全部走 `@/design-system`；颜色/间距取 token；危险操作 `Popconfirm`；Modal `maskClosable={false}`；时间用 `formatDateTime()`；提示用 `App.useApp()`。
- 图表**不引入**新图表库：概览用 `Statistic`/`Card`，趋势日序列用 `DataTable`（日期/次数/tokens），按模型分布用 `DataTable` + `Progress` 占比列。
- i18n：新增 `appWorkspace.*`、`appLogs.*`、`appMonitor.*`、`appAccess.*`、`appDebug.*`，zh-CN / en-US 同步。
- 测试：`ui/src/pages/teams/apps/__tests__/` 下补 `AppWorkspace`、`AppLogsSection`、`AppMonitorSection`、`AppDebugChat` 用例。

## 失败处理

- 调试会话注册表不存在/过期 → 404，前端自动重建一次后重试。
- 调试会话非本人 → 404（不泄露存在性）。
- 应用非 Agent 类型创建调试会话 → 400；已禁用 → 403。
- Member 访问日志/监控/创建调试 → 403；非成员 → 404。
- 日志/监控对不存在或非本团队应用 → 404。
- 调试对话运行异常沿用现有 SSE 错误转文本策略，不新增中断路径。

## 验证

- 后端：`dotnet build src/MoAI/MoAI.csproj` 0 error。
- E2E：扩展 `local-dev/app-e2e.mjs`（或新增 `local-dev/app-workspace-e2e.mjs`），覆盖：未发布应用创建调试会话并对话、调试消息**不落库**、刷新后旧调试会话不可续接、日志分页与全用户可见、监控汇总与按模型分布、Member/非成员拒绝。
- 前端：`npm run typecheck && npm run lint && npm run test`。
- 文档：更新 `docs/app/*`（bdd 新增 `@AP-S*` 场景、tdd 补映射、sdd 增决策/已知问题、sop 排障）；`rounds-log.md` 记账。无 DDL 变更。

## 关键决策

- **D27 工作台分区用左侧菜单**：修订原 D21「管理页不做左侧菜单」——分区从 1 个变 4 个后必须有导航；采用与 `WikiDetail`/`TeamManage` 一致的左侧菜单（`Layout` + `Sider` + `Menu`），配置分区内部再左右分栏（左配置、右调试）。D21 假设「配置项互相有关联、不应分区切换」被「配置外新增日志/监控/访问点」推翻。
- **D28 调试会话复用现有对话链路 + Redis 注册表**：不新建 AG-UI 端点与 store；dispatcher 在 DB 无会话行时回落 Redis 注册表解析。改动面最小、单一对话链路。
- **D29 调试不落库靠「无 session 行」自然成立**：`FlushAsync` 无行即 return，无需新增 `is_draft` 字段或分支；调试会话不计用量（工厂层跳过计数器）。
- **D30 调试会话 TTL 独立于正式热态**：注册表 2h 滑动；前端刷新即弃用 id；残留热态键靠 24h TTL 清理。不追求服务端即时销毁。
- **D31 日志是压缩后视图**：不对压缩丢弃的原文做额外留存；如需原始审计态另立需求。
- **D32 监控基于聚合用量表（无趋势），不做运行期埋点**：直接查 `ai_model_token_audit`（`UseType=App` + `use_resource_id == appId`，该列为 Guid）交付汇总 + 按模型分布；**不含按日趋势**（聚合表无时间分桶），成功率/延迟留后续。
- **D33 访问点本期占位**：仅外部应用可见，先定地址形态与授权口径，生成能力后续迭代。

## 分期计划

1. **工作台外壳 + 配置分区 + 调试会话**：路由/Tab、`AppConfigSection`、`ChatConversation` 抽取、`AppDebugChat`、后端 `debug/session` + dispatcher/factory 改动、E2E。
2. **日志看板**：`QueryAppLogs` 四件套 + 前端 `AppLogsSection` + E2E。
3. **监控看板**：`QueryAppUsage` 四件套 + 前端 `AppMonitorSection` + E2E（基于聚合表 `ai_model_token_audit`，无需迁移；不含按日趋势）。
4. **访问点占位页**：外部应用 `AppAccessSection` 规划说明与地址预览。

每期独立可验证后再进入下一期。

## 后续迭代

1. 访问点：JS 悬浮对话组件 SDK、嵌入代码生成、域名白名单、组件主题定制、启停。
2. 调试配置临时化：未保存表单随调试请求下发并缓存 Redis，支持「一键发布」。
3. 日志：原始消息留存、导出、标注与反馈回流。
4. 监控：运行期埋点（成功率/延迟/QPS）、按用户/外部身份维度、告警。
5. 流程应用的工作台分区（待流程引擎方案确定）。
