# Agent 运行时模块设计规格（SDD）

> 关联：[SDD](./sdd.md) ｜ [BDD](./bdd.md) ｜ [TDD](./tdd.md) ｜ [SOP](./sop.md) ｜ 上游：[../app/sdd.md](../app/sdd.md) ｜ [../wiki/sdd.md](../wiki/sdd.md)

- 日期：2026-09-11
- 状态：后端运行时（AG-UI 对话 / 会话持久化 / 上下文压缩 / 知识库 RAG / 用量）已实现并通过 `dotnet build` 0 错误；发布与会话 CRUD 在 `src/app`；前端对话页已实现并通过 typecheck/lint/test
- 领域：`src/ai`（MoAI.AI.Shared / MoAI.AI.Core），前端 `ui/src/pages/teams/apps/AppChat.tsx`（样式 `app-chat.css`）
- 依赖框架：`Microsoft.Agents.AI.Hosting.AGUI.AspNetCore 1.20.0-preview.260831.1`（Microsoft Agent Framework，AG-UI 协议）
- 本地源码参考：`F:\sourcecode\agent-framework-1.20`（tag `dotnet-1.20.0`，worktree）

## 1. 目标

把「团队应用」变为可对话的 Agent：应用发布后，团队成员进入应用对话；后端用 Microsoft Agent Framework 装配 Agent，以 **AG-UI 协议**（SSE）向前端输出流式事件；对话消息持久化到 `app_agent_message`；每个对话是一个 `app_agent_session`；支持挂靠知识库（RAG）与上下文压缩；统计 token 用量。设计以**可扩展**为目标：模型解析、上下文提供者、工具来源均为可插拔接口。

## 2. 架构

```
React AppChat ──REST(发布会话/历史)──► src/app
              └─AG-UI SSE(fetch)─────► src/ai AppAgentDispatcher
                                        ├ ChatHistoryProvider(Redis热→PG)
                                        ├ AIContextProviders(RAG/压缩) 由 Contributor 组装
                                        ├ AppAgentSessionStore(Redis热+PG冷快照)
                                        └ UsageCapturingChatClient(用量)
src/wiki IWikiSearchService(RAG) ◄──────┘
```

- 端点：`POST /api/agent/{appId}/chat`（`MapAGUIServer`，`RequireAuthorization`，SSE）。
- 单例 `AIAgent`（keyed `moai-app-agent`）为**动态派发** Agent：每个请求按路由 `appId` + 当前用户 + 会话装配真正的 `ChatClientAgent`。
- 会话模型：AG-UI `threadId` == `app_agent_session.id`；前端每轮只发最新用户消息，历史由服务端 Provider 管理。

## 3. 数据与存储

- **热态（Redis，前缀 `moai:`）**：`appagent:msg:{sessionId}` 消息列表、`appagent:session:{sessionId}` 会话快照（AgentSession 序列化，含压缩索引）、`appagent:usage:{sessionId}` token 累计；TTL 24h 滑动。
- **冷态（Postgres）**：
  - `app_agent_message`：**压缩后视图**，运行结束由 flush 用压缩结果物理替换（被压缩的旧消息原文丢弃）。
  - `app_agent_session.state`（jsonb，本次新增）：会话冷快照，Redis 失效后恢复。
  - `app_agent_session` token 累计 / `last_message_time` / 标题由 flush 回写。
- **schema 变更（本模块唯一 DDL）**：见 [SOP](./sop.md) 与 `asserts/app_agent_chat.sql`（`app.publish_status` / `app.publish_time` / `app_agent_session.state`），改后须重跑 `tool/PostgresScaffold`。
- 实体/EF 映射放在 `src/database/.../Partial/` 的 partial 文件，避免 rescaffold 覆盖。

## 4. 组件（`src/ai`）

| 组件 | 职责 |
|---|---|
| `AppAgentDispatcher : DelegatingAIAgent` | 按请求装配内层 Agent，会话方法委托给会话壳 `ChatClientAgent` |
| `AppAgentFactory` | 装配模型 + 提示词 + 历史 Provider + 上下文管线 |
| `IAiModelResolver` / `AiModelResolver` | 模型按团队可用性解析（与配置保存同口径） |
| `AppChatHotStore` | Redis 热态读写（消息/快照/用量） |
| `PostgresChatHistoryProvider : ChatHistoryProvider` | 命中 Redis，未命中回表并回填；仅落 External 请求消息 |
| `AppAgentSessionStore : AgentSessionStore` | `threadId`→会话态；保存后触发 flush |
| `AppChatFlushService` | 压缩 + 落库 + 会话聚合/快照回写 |
| `AppCompactionStrategyFactory` | 按 `execution_settings` 组装压缩管线 |
| `IAppContextProviderContributor` / `AppContextProviderFactory` | 可插拔上下文装配（新增能力不改工厂） |
| `AppToolContextProviderContributor` | 聚合工具来源，装配「渐进式工具披露」元工具 Provider（Order=20） |
| `AppToolContextProvider` | 暴露 `list_tools` / `call_tool` 两个元工具；不把全部工具注入每轮 |
| `IAppToolProvider` / `PluginAppToolProvider` / `WikiAppToolProvider` | 工具来源：插件（静态/动态/MCP/OpenAPI）与知识库 |
| `GraphAppToolProvider` | 工具来源：知识图谱——`search_knowledge_graph`（Kind=graph），仅应用绑定 `GraphIds` 非空时产出（SP-A） |
| `SandboxAppToolProvider` | 工具来源：沙箱（代码/shell/文件），仅应用开启沙箱时产出 |
| `IAppSandboxService` / `OpenSandboxService` | 会话沙箱：惰性创建/复用/续期/销毁 + 代码/命令/文件执行（封装 `Alibaba.OpenSandbox`） |
| `SandboxReaperJob` + `SandboxReaperRegistrationService` | Hangfire 周期回收孤儿沙箱（每 5 分钟） |
| `CompactionContextProviderContributor` | 压缩 Provider（置于管线最后） |
| `UsageCapturingChatClient : DelegatingChatClient` | 捕获用量 → 会话热态 + `IAiModelUsageCounter`（`AiModelUseType.App`） |

`src/wiki` 提供 `IWikiSearchService`（按知识库 embedding 模型生成查询向量并召回）；`src/knowledgegraph` 提供 `IGraphSearchService`（向量召回 + 一跳邻接扩展 + 子图文本化，SP-A）；`src/aiplugin/MoAI.AIPlugin.Custom` 新增 `McpToolCallService` / `OpenApiToolCallService`（自定义插件的运行时调用，补齐此前仅有存储、无调用的缺口）；`src/app` 新增发布命令与会话 CRUD/CQRS。

### 4.1 工具与知识库（渐进式披露）

- **工具来源**：`PluginAppToolProvider` 读取 `AppAgentConfigEntity.Plugins`，按 `PluginEntity.Type` 解析为工具：
  - `nativePlugin`：静态（`plugin_statics.PluginKey` → 注册表）或动态实例（`plugin_dynamics` → 模板 + 配置）→ `IPluginExecutor`。
  - `mcp`：`plugin_customs` + `plugin_functions`，每个 MCP 工具一个工具项 → `McpToolCallService`（`McpClient.CallToolAsync`）。
  - `openapi`：每个 operation 一个工具项 → `OpenApiToolCallService`（按已上传文档的 method/path/参数发起 HTTP）。
  `WikiAppToolProvider` 读取 `WikiIds`，生成 `search_knowledge_base` 工具 → `IWikiSearchService`；`GraphAppToolProvider`（Order=21）读取 `GraphIds`（本团队托管图谱），生成 `search_knowledge_graph` 工具（Kind=graph）→ KG 模块 `IGraphSearchService`。入参 `{query（必填）, topK?}`（topK 钳 1-20、默认 5，**为每张绑定图谱各自的召回数**）；工具描述引导模型「适合 A 和 B 什么关系类问题、仅返回一跳关系、要文档原文改用知识库检索」。返回 payload：`{query, count, hits[{graphId, nodeId, name, entityType, description, score, neighbors[{relation, direction, name, description}]}], skipped[], hitsTruncated?}`——总长超 **16KB** 预算即停止追加命中并置 `hitsTruncated=true`；未配置向量化/模型不可用的绑定图计入 `skipped` 可读提示回给模型（不阻断其余图）。
- **渐进式披露**：`AppToolContextProvider` 只注入 `list_tools(query?)` 与 `call_tool(toolName, argumentsJson)` 两个元工具；模型先 `list_tools` 获取工具名与参数示例（MCP 参数 Schema 惰性拉取），再 `call_tool` 调用，避免把全部工具定义塞进每轮上下文。
- **扩展点**：新增工具类型只需实现 `IAppToolProvider`（Maomi 自动注册），聚合与元工具无需改动。
- **命名与去重**：MCP/OpenAPI 工具名为 `{插件名}__{函数名}`；同应用内按工具名去重。

### 4.2 沙箱（OpenSandbox）

- **配置存 JSON**：全局 `SystemOptions.OpenSandBox`（二级对象 `SystemOptionSandBox`：`Address/ApiKey/Image/TimeoutSeconds/RenewThresholdSeconds`，**镜像属系统设置，应用不可改**）；应用级写 `app_agent_config.execution_settings.sandbox`（`enabled/timeoutSeconds/renewOnAccess/resource{cpu,memory}/network{defaultAction,egress[]}`），**不改表结构**。
- **每会话一沙箱**：Redis `appagent:sandbox:{sessionId}` 记录 `{sandboxId, expiresAt}`；首次沙箱工具调用惰性 `Sandbox.CreateAsync`（镜像 + entrypoint `/opt/code-interpreter/code-interpreter.sh` + 元数据 `moai.session/app/team`），后续调用 `Sandbox.ConnectAsync` 复用；剩余存活时间低于阈值且 `renewOnAccess` 时自动续期。
- **工具**（经 `list_tools`/`call_tool` 渐进披露）：`sandbox_run_code`（Jupyter，python/java/go/typescript/javascript/bash）、`sandbox_run_shell`、`sandbox_write_file`、`sandbox_read_file`、`sandbox_list_dir`、`sandbox_delete_file`、`sandbox_search_files`；执行结果统一映射为 `{success,stdout,stderr,results,exitCode,error}`。
- **回收**：会话/应用删除时尽力 `KillSessionAsync`；沙箱自身 TTL + Hangfire `SandboxReaperJob`（按 `moai.` 元数据对账，清理无主沙箱）兜底。
- **扩展点**：沙箱作为 `IAppToolProvider` 实现接入既有工具目录，装配无需改动。


## 5. 关键决策

- **D1 动态派发**：`MapAGUIServer` 启动期固定 Agent 实例，故用单例派发 Agent + 每请求 scope 装配，绕开「Agent 与应用一一绑定」的限制。
- **D2 threadId = 会话 id**：AG-UI 续聊标识直接复用 `app_agent_session.id`，前端每轮仅发最新消息。
- **D3 热态 Redis + 冷快照**：运行期只写 Redis 提升多轮性能；完成后一次性刷 Postgres；`AgentSession.StateBag`（含压缩索引）随会话写入 Redis 与 `app_agent_session.state`。
- **D4 落库即压缩**：flush 用 `CompactionProvider.CompactAsync` 对全量消息压缩后物理替换 `app_agent_message`；**被压缩行原文丢弃、不可回看**（用户确认）。
- **D5 默认无损压缩**：`ToolResult + SlidingWindow + Truncation`；`execution_settings.enableSummarization=true` 时才引入 LLM 摘要（多一次模型调用）。
- **D6 Provider 来源归因**：历史 Provider 仅存 External 请求消息，避免 RAG/压缩注入被重复落库。
- **D7 会话越权防护**：派发与会话 REST 均校验 `create_user_id`，非归属按 404（不泄露存在性）。
- **D8 发布状态落库**：`app.publish_status`（0 草稿 / 1 已发布）+ `publish_time`；仅 Admin+ 可发布，仅 Agent 应用可发布，Member 仅能对已发布应用建会话。
- **D9 专用端点是 REST 之外的 SSE**：AG-UI 使用 `fetch` + SSE，不走 Kiota；REST（发布/会话）仍走 Kiota。
- **D10 知识库 RAG 按需检索**：知识库以工具形式提供（`search_knowledge_base`），经 `list_tools`/`call_tool` 调用；不再单独挂 `TextSearchProvider`，避免与工具目录重名。
- **D11 沉浸式对话页**：`AppChat` 不套 `Page`（去面包屑/大标题），采用「左侧会话列表 + 右侧对话流」的 Open-WebUI 式布局；样式独立放 `app-chat.css`，颜色一律由组件注入的 CSS 变量（取自 antd token）驱动，兼容明暗主题，**不引入业务逻辑里的 hex 硬编码**；流式助手消息用 `react-markdown` + `remark-gfm` 渲染，含复制、停止、打字指示、工具调用 chip 与响应式抽屉侧栏。
- **D12 渐进式工具披露**：不把绑定插件/知识库的全部工具定义注入每轮请求，而是暴露 `list_tools`（按需加载/筛选）与 `call_tool`（按名调用）两个元工具；降低上下文膨胀与工具误选，工具数量增长时仍稳定。
- **D13 工具来源可插拔**：以 `IAppToolProvider` 抽象来源，工厂按 `Order` 聚合并按名去重；新增 MCP/OpenAPI/知识库等能力只加实现，不改装配。
- **D14 自定义插件补齐运行时**：MCP/OpenAPI 此前仅能存储与列举、无调用路径；本轮新增 `McpToolCallService`（`McpClient.CallToolAsync`）与 `OpenApiToolCallService`（重读已上传文档、按 method/path/参数发起 HTTP）。
- **D15 沙箱选 OpenSandbox 而非 MAF**：MAF 的 Docker 能力仅限 shell（无代码解释器）、持久容器单会话单用户、无多租户服务端；OpenSandbox 提供代码解释器 + 命令 + 文件 + 生命周期，且服务端独立部署，贴合 MoAI 多团队多会话。故仅接 OpenSandbox（其 .NET SDK `Alibaba.OpenSandbox` / `.CodeInterpreter`）。
- **D16 沙箱配置走 execution_settings JSON**：应用是否开沙箱等扩展配置写入 JSON 对象，**不新增数据库列**，后续扩展无需改表与 rescaffold；Save/Query 命令透传 `executionSettings`（null 表示不覆盖，避免旧前端清空）。
- **D17 每会话一沙箱 + TTL**：`threadId`（会话）粒度；首次用到沙箱工具才创建（惰性），同会话复用，剩余 TTL 不足自动续期；沙箱自身 TTL 与 Hangfire 回收任务双保险，避免资源泄漏。
- **D18 流程对话压缩独立于 Agent 管线**（2026-09-20）：流程应用（Workflow）不走 `ChatClientAgent` 的 AIContextProviders（常规框架压缩不生效），其 `sys.history` 在 `WorkflowAppChatInvoker`（Workflow.Core）内**单独使用 MAF 压缩组件**：同一 `AppCompactionStrategyFactory` 按 `execution_settings` 组装策略，静态 `CompactionProvider.CompactAsync` 压缩后注入；压缩异常退回最近 20 条原文。策略工厂因此被 Agent 运行时/落库 flush/流程对话三处共用。

## 6. 已知问题 / 下阶段

- **DDL 未自动演进**：存量库须手工执行 `asserts/app_agent_chat.sql` 并重跑 PostgresScaffold，否则应用相关查询会因缺列报错。
- **压缩无 tokenizer**：压缩按估算 token 计数（未接 `Microsoft.ML.Tokenizers`），阈值偏保守。
- **flush 崩溃窗口**：Redis 落 PG 前进程崩溃会丢当轮消息（未接补偿任务）。
- **压缩为 agent 级**：`AIContextProviders` 于每次 run 前执行，未进入 function-calling 循环内部。
- **MCP/OpenAPI 调用无长连接与缓存**：每次 `call_tool` 新建 MCP 连接；OpenAPI 每次调用重读并解析已上传文档，未做缓存（后续可加）。
- **OpenAPI 仅支持 path/query/header 参数与 JSON body**：复杂 body schema（数组/嵌套必填/表单/文件）按原样透传，未做深度校验。
- **工具执行无审批与超时细粒度控制**：MCP 连接沿用服务默认超时；写操作类工具未接人工审批（Human-in-the-loop）。
- **沙箱无跨实例分布式锁**：创建锁为进程内 `SemaphoreSlim`，多实例部署需换分布式锁。
- **沙箱删除联动未硬保证**：`src/app` 不反向依赖 `src/ai`，会话/应用删除的沙箱销毁依赖 reaper 与 TTL 兜底。
- **沙箱出站网络策略未在 UI 暴露**：可在 sandbox 配置中扩展 `networkPolicy`（服务端支持）。
- **沙箱镜像拉取首次较慢**：已把 SDK 请求超时放宽到 600s、就绪等待 300s；镜像建议在沙箱服务端预热。
- 摘要模型配置入口、Rerank、外部用户使用、并发同一会话的锁、MCP/OpenAPI/沙箱 E2E 自动化仍可补强。
- 会话列表仅返回当前用户自己的会话；管理员查看他人会话未开放。
