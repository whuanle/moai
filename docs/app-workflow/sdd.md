# 流程应用模块设计规格（SDD）

> 关联：[SDD](./sdd.md) ｜ [BDD](./bdd.md) ｜ [TDD](./tdd.md) ｜ [SOP](./sop.md) ｜ 上游：[../app/sdd.md](../app/sdd.md) ｜ 证据：[local-dev/workflow-e2e.mjs](../../local-dev/workflow-e2e.mjs)

- 日期：2026-09-16（首版：执行引擎迁移自 `F:\demo\worktest` 验证项目，前端设计器迁移自 moai_old 的 FlowGram 画布并重写为「画布单一数据源」架构）
- 状态：后端（引擎 + CQRS 三层 + EF 存储 + 端口实现）与前端（设计器 + 运行历史）已实现，E2E 34/34 通过（2026-09-17）
- 领域：`src/app/workflow/`（引擎类库 + Shared/Core/Api 四项目），前端 `ui/src/pages/teams/apps/workflow/`
- Schema 真源：`src/database/MoAI.Database.Postgres/Data/AppWorkflow*.cs`；DDL：[asserts/app_workflow.sql](../../asserts/app_workflow.sql)

## 1. 目标

流程应用（`app_type=1`）以**可视化 DAG 编排**定义 AI 应用：团队管理员在 FlowGram 自由布局画布上拖拽节点、连线、配置参数，保存草稿、发布生成不可变版本快照，并**同步调试执行**查看节点级状态。引擎已实现 7 种节点：`start / end / condition / switch / aiChat / javaScript / plugin`（fork/forEach/wiki/dataProcess 留待后续迭代）。一期「协作」指设计器与后端联动（拖拽→保存→发布→调试→运行历史），不做多人实时协同。

## 2. 组件与数据流

```
ui/src/pages/teams/apps/workflow/        FlowGram FreeLayoutEditor 画布（编辑期唯一数据源 = toJSON）
  ├─ WorkflowDesigner.tsx                页头（FastGPT 风格：返回/状态行/操作钮）+ 画布装配/节点注册（含 condition 双出边端口 true/false）+ 左浮动工具列装配
  ├─ NodePanel.tsx                       节点库浮层（「添加节点」唤出、← 收起，分组两列网格，拖拽投放画布）
  ├─ SystemSettingsPanel.tsx             系统设置浮层（对话开场白开关+文案、全局变量编辑）
  ├─ RunPanel / VariablesDrawer          调试运行右侧抽屉 / 全局变量编辑体（被系统设置面板复用）
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
- **D11 开始/条件节点透传语义**（2026-09-17 迭代）：开始节点「输入什么就输出什么」——不声明输出字段，启动 JSON 原样作为 start 输出（引擎不再校验必需启动参数，除非定义仍声明 outputs）；条件节点输出 = 输入透传 + `result` 字段，下游可引用透传字段。设计器对这两类节点隐藏通用输入/输出编辑器：开始节点仅显示透传说明；条件节点显示专属「条件判断（布尔）」绑定编辑器（variable/fixed/interpolation + 上游变量提示）。
- **D12 条件分支可见性**：条件节点双出边端口分离——true 在节点顶部、false 在右侧（`CONDITION_PORTS` 带 location），节点体渲染「真↑/假→」徽标，避免用户分不清分支走向。
- **D13 插件输出智能填充**：`QueryTeamPluginsCommandResponse` 的插件项新增 `responseSchema`（`PluginTypeHelper.GetResponseSchema` 反射响应类型顶层属性生成 name/fieldType/description）；设计器选择插件后自动覆盖输出参数（schema 为空则保留），并提示可手动调整。输入绑定支持字段类型（引擎 `FieldBinding.FieldType` 仅作设计器元数据）。
- **D14 上游变量智能提示**：variable/jsonpath 表达式的值输入用 AutoComplete 列出 `sys.*` 与全部祖先节点输出（节点名.字段）；interpolation 提供「插入变量」追加 `{引用}` 到模板。
- **D15 连线序列化格式（重要）**：FlowGram `document.toJSON()` 把全部连线序列化在**顶层 `edges`**（节点内不含）；而**加载**节点内 edges 时按节点顺序即时创建出边，目标节点若排在数组后面会被 `createWorkflowLine` 静默丢弃（连线消失）。因此：①保存转换 `fromEditorFormat`/校验/上游分析统一走 `collectEdges`（合并节点内与顶层两处来源）；②加载用 `normalizeEditorData` 把任意历史格式规范化为「节点内无 edges + 全部连线提升到顶层」，顶层边在全部节点创建完后统一创建，顺序无关。
- **D17 设计器外壳 FastGPT 化**（2026-09-17 迭代）：`AppWorkspace` 对 design 分区**提前返回**，不渲染 `Page` 面包屑与「返回应用列表」页头；设计器自带头部——左侧「返回箭头（回应用列表）+ 应用名 + 状态行（草稿/已发布圆点 · vN · 未保存红点）」，右侧「运行历史（图标钮）｜调试运行、保存、发布（主钮）」。画布**真全屏**：`AppLayout` 对 `/team/:teamId/app/:appId/design` 路由去全局 24px 内边距（`FULLSCREEN_PATH`），`.wf-designer` 高度 `100vh`、无描边圆角。
- **D18 左浮动工具列 + 侧边浮层面板 + 系统设置**（2026-09-17 迭代）：节点库不再固定占位——画布左上浮动工具列（黑色「+ 添加节点」、白色「⚙ 系统设置」）唤出互斥浮层（`.wf-side-panel`，覆盖画布左侧、`pointer-events` 穿透不挡画布），面板头 ← 收起；「系统设置」含**对话开场白**（开关 + ≤4000 文案）与**全局变量**（复用变量编辑体），头部「全局变量」按钮与右侧变量 Drawer 移除。**开场白存储决策**：复用 `app_agent_config` 行与 agent-config 接口（`SaveAppAgentConfigCommandHandler` 对 Workflow 应用放行且只写 `OpeningStatement/OpeningStatementEnabled`，模型/知识库/插件/技能不校验不落库；`QueryAppCommandHandler` 详情下发条件放宽到 Workflow），API 契约零变更、免 syncapi；前端 `store.save()` 在草稿保存后同步提交开场白（请求失败整体报错可重试），`load()` 并行拉取且查询失败不阻塞设计器。
- **D19 节点信息与输出参数可编辑**（2026-09-17 迭代）：①节点卡片头部标题/副标题**点击就地编辑**（`EditableNodeText`：点击进输入态，Enter/失焦提交、Esc 取消，悬浮显铅笔图标）——分别写 `data.title`/`data.content`（往返映射引擎 `name`/`description`），副标题为空回退类型文案；引用走节点 id，改名不影响连线索引与上游变量提示；②JavaScript/插件节点「输出参数」由只读展示改为可编辑（名称/类型/描述/增删，`OutputsEditor`）——输出是下游引用的声明元数据，脚本/插件实际返回仍由用户保证；aiChat 输出保持只读（引擎固定 `answer`）。表单体内不重复放名称/描述输入（93 轮曾放三输入框，按反馈收敛）。已知缺口：aiChat 的模型选择器与 plugin 的插件选择器在 86 轮内嵌改版中未迁入节点表单（`settings.aiModelId/pluginKey` 无 UI 入口），待补。
- **D20 节点 Key 可编辑**（2026-09-17 迭代）：节点 Key（引用前缀，如 `search.hasResult` 的 `search`）在「节点信息」区展示并可编辑——画布节点 id 是 FlowGram 内部标识不可变，故自定义 key 存 `data.key` 覆盖：`fromEditorFormat` 以 `data.key ?? id` 为引擎 key，**连线/坐标/引用值统一经 id→key 映射换算**（variable/jsonpath 前缀与 interpolation 花括号自动重写）；`validateEditorData` 校验 Key 格式（`[A-Za-z_][A-Za-z0-9_]*`）/唯一性/保留字（sys/system/input），引用前缀按 id 或 Key 均可回溯祖先；start/end 为固定单例 Key 不可改。加载侧新增 `normalizeReferences`：画布快照（draftEditorData）里旧 id 前缀的引用在进入画布前统一改写为有效 Key，显示与定义一致。
- **D21 条件节点脚本模式 + 表达式类型选择**（2026-09-17 迭代）：①条件节点新增「条件模式」切换——变量引用（原绑定模式，condition 输入绑定求值）/ JS 脚本（config.conditionScript，Jint 执行约定 function condition(inputs, sys, nodes, system) 返回布尔，存在时优先于绑定；输出仍 = 输入透传 + result）；脚本模式隐藏绑定区、切回保留原绑定数据。②绑定行值编辑器前加表达式类型选择（变量/JSONPath/插值/固定值，对应引擎 ExpressionEvaluator 已支持的四类），JSONPath 在 {sys,input,nodes} 上下文求值；此前 UI 无表达式类型入口、只能用模板默认的 variable。③ui sanitizeSettings 放行 conditionScript；i18n conditionMode* / conditionScriptHint；e2e WF-16a/b 双分支 + 引擎单测 14/14。
- **D22 多条件节点（switch/if-else）+ 条件分支绑定 + 绑定表达式类型选择**（2026-09-17 迭代）：①新增 `switch` 节点（NodeTypes.Switch + SwitchNodeExecutor）——config.branches=[{id,label,binding}] 按顺序用 IExpressionEvaluator 求值，第一个真值分支命中：输出=输入透传+{result: 分支 id}，全部未命中 result="else"；调度器新增按 result 字符串匹配出边 condition 标记；验证器校验出边标记 ∈ 分支 id ∪ {else} 且不重复、至少一个分支。②前端 switch 节点用 **FlowGram 动态端口**（meta.useDynamicPort + 分支行渲染 [data-port-id] 锚点 + 分支数变化时 updateAllPorts），分支可增删/命名/选表达式类型；sanitizeBinding 抽出复用、fromEditorFormat/toEditorFormat 支持 branches 往返。③条件节点绑定模式表单精简（去掉 condition 字段名/必填/添加输入）并新增**分支绑定**：显示两条出边目标，「满足时走哪条」存 config.trueTarget（布局：满足时/不满足时固定顺序在左、绑定的节点在右）（目标节点 key），fromEditorFormat 据此写出边 condition 标记（trueTarget 随 id→key 重写、失效时回退端口标记），引擎零改动；④绑定行新增表达式类型选择（变量/JSONPath/插值/固定值，引擎 ExpressionEvaluator 已支持）；⑤验证——引擎单测 14/14、workflow-e2e **38/38**（WF-16 脚本条件、WF-17 多条件双分支）、前端 318/318、浏览器实测拖入 switch/动态端口/分支增删/条件分支绑定交换（2026-09-17）。


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

- **D16 流程全局变量**（2026-09-17 迭代）：`WorkflowDefinition.Variables`（`GlobalVariableDefinition`：name/fieldType/defaultValue/description）；作用域新增 `system.*` 命名空间（`WorkflowVariableScope.WorkflowGlobals`，与 `sys.*` 系统变量分立）；启动时按「定义默认值 ← 调用方传入值」合并并随 `WorkflowInstance.SystemVariables` 持久化（断点恢复后作用域可重建）；JS 沙箱 `run` 增加第 4 参 `system`（旧三参脚本兼容）；调试命令 `SystemJson` 传参；设计器 header「全局变量」抽屉声明变量、调试面板按声明赋值、变量提示含 `system.*`。验证：引擎单测（默认值/覆盖/实例持久化）、e2e WF-14a/b、浏览器实测。

## 6. 已知问题与后续迭代

- fork/forEach/wiki/dataProcess 节点未实现（前端枚举与常量结构已预留）。
- 调试执行为同步请求（长流程受 HTTP 超时约束）；SSE 事件流式与实例事件持久化为二期。
- 正式执行入口（发布应用的对外调用/会话化）未开放，一期以设计器调试 + 运行历史为主。
- 设计器多人实时协同（Yjs）未纳入本期。
- 调试执行为「隐式保存草稿再跑」，运行历史中调试实例的输入为面板当前值；正式执行入口见 §6 第三条。
