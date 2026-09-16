# 流程应用模块设计规格（SDD）

> 关联：[SDD](./sdd.md) ｜ [BDD](./bdd.md) ｜ [TDD](./tdd.md) ｜ [SOP](./sop.md) ｜ 上游：[../app/sdd.md](../app/sdd.md) ｜ 证据：[local-dev/workflow-e2e.mjs](../../local-dev/workflow-e2e.mjs)

- 日期：2026-09-16（首版：执行引擎迁移自 `F:\demo\worktest` 验证项目，前端设计器迁移自 moai_old 的 FlowGram 画布并重写为「画布单一数据源」架构）
- 状态：后端（引擎 + CQRS 三层 + EF 存储 + 端口实现）与前端（设计器 + 运行历史）已实现，E2E 27/27 通过
- 领域：`src/app/workflow/`（引擎类库 + Shared/Core/Api 四项目），前端 `ui/src/pages/teams/apps/workflow/`
- Schema 真源：`src/database/MoAI.Database.Postgres/Data/AppWorkflow*.cs`；DDL：[asserts/app_workflow.sql](../../asserts/app_workflow.sql)

## 1. 目标

流程应用（`app_type=1`）以**可视化 DAG 编排**定义 AI 应用：团队管理员在 FlowGram 自由布局画布上拖拽节点、连线、配置参数，保存草稿、发布生成不可变版本快照，并**同步调试执行**查看节点级状态。一期支持引擎已实现的 6 种节点：`start / end / condition / aiChat / javaScript / plugin`（fork/forEach/wiki/dataProcess 留待后续迭代）。一期「协作」指设计器与后端联动（拖拽→保存→发布→调试→运行历史），不做多人实时协同。

## 2. 组件与数据流

```
ui/src/pages/teams/apps/workflow/        FlowGram FreeLayoutEditor 画布（编辑期唯一数据源 = toJSON）
  ├─ WorkflowDesigner.tsx                画布装配/节点注册（含 condition 双出边端口 true/false）
  ├─ NodePanel / ConfigPanel / FieldsEditor / RunPanel
  └─ utils.ts                            编辑器 JSON ↔ 引擎 WorkflowDefinition 双向转换 + 客户端校验
src/app/workflow/
  ├─ MoAI.App.Workflow/                  引擎类库（纯 .NET，宿主注入存储与端口）
  │   ├─ Instance/   WorkflowEngine（门面 Start/StartWithDefinition/Resume/Cancel）+ WorkflowScheduler（DAG 调度）
  │   ├─ Definition/ WorkflowDefinition 契约 + WorkflowValidator + WorkflowCompiler
  │   ├─ DataTransfer/ 变量作用域 + 表达式求值（run/fixed/variable/jsonpath/interpolation）
  │   ├─ Nodes/      INodeExecutor + 6 个内置执行器（Jint JS 沙箱；aiChat/plugin 走端口）
  │   └─ Persistence/ 存储抽象 + InMemory（单测用）
  ├─ MoAI.App.Workflow.Shared/Core/Api   CQRS：草稿保存/发布/调试执行/配置与实例查询（角色门禁在 Handler）
  └─ Core/Services|Stores                端口与存储实现（见 §3）
```

## 3. 关键决策

- **D1 引擎对象图 Scoped**：调度器每节点检查点落库依赖 EF 实例存储（Scoped），故引擎/调度器/事件发布器/执行器注册为 Scoped；编译器/验证器无状态保持 Singleton。
- **D2 定义 id = 应用 id**：流程编排与 app 1:1（`app_workflow_config.app_id` partial 唯一），引擎的 `DefinitionId` 即应用 id 字符串，免去多一层定义表。
- **D3 调试走 `StartWithDefinitionAsync`**：引擎新增入口直接执行给定定义对象（草稿），不经"已发布定义"查询；实例自含定义快照（`InstanceData`），断点恢复不依赖草稿现状。
- **D4 执行上下文（Scoped）补齐宿主维度**：引擎接口不含团队信息，`WorkflowExecutionContext`（TeamId/ConfigId/IsDebug）由 Handler 在调用引擎前写入，EF 存储与 AI/插件端口据此落库与解析团队资源。
- **D5 端口对接既有基础设施**：`IAiChatClient` → `IAiModelResolver` + `IChatClientProvider`（流式片段回调引擎进度事件）；`IWorkflowPluginInvoker` → `IMediator.Send(RunPluginCommand)`（静态/动态插件统一入口）。aiChat 节点 `config.aiModelId` 由前端团队网关模型选择器写入。
- **D6 画布单一数据源**：保存/调试时由画布 `toJSON()` 经 `fromEditorFormat` 生成引擎契约；引擎契约里的 `ui.nodePositions` 仅随定义存储，画布还原优先用 `draftEditorData`（FlowGram 原始 JSON）无损恢复。条件节点双出边用 FlowGram 命名端口（`portID: 'true'/'false'`），连线 `sourcePortID` 直转引擎 `connection.condition`。
- **D7 引擎迁移修复三缺陷**（迁移过程中由单测暴露，worktest 原样存在）：①插值表达式对字符串值误用 `ToJsonString()`（带 JSON 引号）；②验证器对重复节点 Key 抛 `ArgumentException` 而非报错；③断点恢复时已 Skipped 节点不重新传播跳过（恢复后下游永不就绪，误判为 Completed 空输出）。
- **D8 事件日志一期进程内**：同步调试返回的实例快照已含节点级状态，`IWorkflowEventLogStore` 用进程内实现；持久化事件表与 SSE 实时流式留待后续。
- **D9 前端集成三约束**（浏览器实机走查定位）：①FlowGram 的 IoC 容器与 React 19 StrictMode 双挂载冲突（`Ambiguous match: FlowRendererRegistry`）——`main.tsx` 不包 StrictMode，且 `vite.config.ts` 对 `@flowgram.ai/*` 加 `resolve.dedupe` + `optimizeDeps.include` 防双实例；②添加节点必须走 `document.createWorkflowNode`（走工作流专属实体/端口初始化与内容变更事件），`operation.addNode` 只改模型不进渲染层；③节点实体类型取 `node.flowNodeType`（`node.type` 是实体类型）。
- **D10 GET 禁用浏览器缓存**：保存后重新加载配置可能命中启发式缓存拿到旧响应，Kiota `FilterRequestHandler` 对所有 GET 统一加 `Cache-Control: no-cache`。

## 4. 数据模型

- `app_workflow_config`（与 app 1:1）：`draft_definition`（引擎契约 JSON）、`draft_editor_data`（FlowGram toJSON）、`published_definition`（发布快照，不可变）、`version`（发布一次 +1，0=从未发布）、`status`（0=草稿有变更 1=草稿与已发布一致）、`publish_time` + 审计。
- `app_workflow_instance`（运行实例）：`workflow_config_id`、`version`（0=调试）、`is_debug`、`status`（0=创建 1=执行中 2=挂起 3=完成 4=取消）、`input`/`output`/`error_message`、`instance_data`（引擎 `WorkflowInstance` 全量 JSON，含每节点状态/输入/输出/Attempts）。

## 5. 接口（`/api/app/workflow`，Controller 组装 + `SetUserContext`）

| 方法 | 路径 | 权限 | 说明 |
|---|---|---|---|
| POST | `/draft` | Admin+ | 保存草稿（definition + editorData，轻校验，status→0） |
| POST | `/publish` | Admin+ | `WorkflowValidator` 全量校验 → 快照发布（version+1）→ 应用置为已发布 |
| POST | `/debug-run` | Admin+ | 隐式保存草稿 → 编译校验 → 同步执行到终态，返回节点级状态 |
| GET | `/config` | Member+ | 查询配置（草稿/已发布定义 + 编辑器 JSON + 版本状态） |
| GET | `/instances` | Admin+ | 分页运行历史（`AuditsInfo` + `IUserInfoFillService` 填充触发人） |
| GET | `/instance` | Admin+ | 实例详情（反序列化 `instance_data` 输出节点级状态） |

## 6. 已知问题与后续迭代

- fork/forEach/wiki/dataProcess 节点未实现（前端枚举与常量结构已预留）。
- 调试执行为同步请求（长流程受 HTTP 超时约束）；SSE 事件流式与实例事件持久化为二期。
- 正式执行入口（发布应用的对外调用/会话化）未开放，一期以设计器调试 + 运行历史为主。
- 设计器多人实时协同（Yjs）未纳入本期。
- 调试执行为「隐式保存草稿再跑」，运行历史中调试实例的输入为面板当前值；正式执行入口见 §6 第三条。
