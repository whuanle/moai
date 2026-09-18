# 流程应用模块设计规格（SDD）

> 关联：[SDD](./sdd.md) ｜ [BDD](./bdd.md) ｜ [TDD](./tdd.md) ｜ [SOP](./sop.md) ｜ 上游：[../app/sdd.md](../app/sdd.md) ｜ 证据：[local-dev/workflow-e2e.mjs](../../local-dev/workflow-e2e.mjs)

- 日期：2026-09-16（首版：执行引擎迁移自 `F:\demo\worktest` 验证项目，前端设计器迁移自 moai_old 的 FlowGram 画布并重写为「画布单一数据源」架构）
- 状态：后端（引擎 + CQRS 三层 + EF 存储 + 端口实现）与前端（设计器 + 运行历史）已实现，E2E 82/82 通过（2026-09-18）
- 领域：`src/app/workflow/`（引擎类库 + Shared/Core/Api 四项目），前端 `ui/src/pages/teams/apps/workflow/`
- Schema 真源：`src/database/MoAI.Database.Postgres/Data/AppWorkflow*.cs`；DDL：[asserts/app_workflow.sql](../../asserts/app_workflow.sql)

## 1. 目标

流程应用（`app_type=1`）以**可视化 DAG 编排**定义 AI 应用：团队管理员在 FlowGram 自由布局画布上拖拽节点、连线、配置参数，保存草稿、发布生成不可变版本快照，并**同步调试执行**查看节点级状态。引擎已实现 10 种节点：`start / end / condition / switch / aiChat / javaScript / plugin / knowledgeSearch / questionClassifier / http`（fork/forEach/dataProcess 留待后续迭代）。一期「协作」指设计器与后端联动（拖拽→保存→发布→调试→运行历史），不做多人实时协同。

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
  │   ├─ Nodes/      INodeExecutor + 10 个内置执行器（Jint JS 沙箱；aiChat/plugin/knowledgeSearch/questionClassifier 走端口；http 内置 HttpClient 出站）
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
- **D19 节点信息与输出参数可编辑**（2026-09-17 迭代）：①节点卡片头部标题/副标题**点击就地编辑**（`EditableNodeText`：点击进输入态，Enter/失焦提交、Esc 取消，悬浮显铅笔图标）——分别写 `data.title`/`data.content`（往返映射引擎 `name`/`description`），副标题为空回退类型文案；引用走节点 id，改名不影响连线索引与上游变量提示；②JavaScript/插件节点「输出参数」由只读展示改为可编辑（名称/类型/描述/增删，`OutputsEditor`）——输出是下游引用的声明元数据，脚本/插件实际返回仍由用户保证；aiChat 输出保持只读（引擎固定 `answer`）。表单体内不重复放名称/描述输入（93 轮曾放三输入框，按反馈收敛）。已知缺口：aiChat 的模型选择器在 86 轮内嵌改版中未迁入节点表单（`settings.aiModelId` 无 UI 入口），待补；plugin 的插件选择器已于 D26 补齐。
- **D20 节点 Key 可编辑**（2026-09-17 迭代）：节点 Key（引用前缀，如 `search.hasResult` 的 `search`）在「节点信息」区展示并可编辑——画布节点 id 是 FlowGram 内部标识不可变，故自定义 key 存 `data.key` 覆盖：`fromEditorFormat` 以 `data.key ?? id` 为引擎 key，**连线/坐标/引用值统一经 id→key 映射换算**（variable/jsonpath 前缀与 interpolation 花括号自动重写）；`validateEditorData` 校验 Key 格式（`[A-Za-z_][A-Za-z0-9_]*`）/唯一性/保留字（sys/system/input），引用前缀按 id 或 Key 均可回溯祖先；start/end 为固定单例 Key 不可改。加载侧新增 `normalizeReferences`：画布快照（draftEditorData）里旧 id 前缀的引用在进入画布前统一改写为有效 Key，显示与定义一致。
- **D21 条件节点脚本模式 + 表达式类型选择**（2026-09-17 迭代）：①条件节点新增「条件模式」切换——变量引用（原绑定模式，condition 输入绑定求值）/ JS 脚本（config.conditionScript，Jint 执行约定 function condition(inputs, sys, nodes, system) 返回布尔，存在时优先于绑定；输出仍 = 输入透传 + result）；脚本模式隐藏绑定区、切回保留原绑定数据。②绑定行值编辑器前加表达式类型选择（变量/JSONPath/插值/固定值，对应引擎 ExpressionEvaluator 已支持的四类），JSONPath 在 {sys,input,nodes} 上下文求值；此前 UI 无表达式类型入口、只能用模板默认的 variable。③ui sanitizeSettings 放行 conditionScript；i18n conditionMode* / conditionScriptHint；e2e WF-16a/b 双分支 + 引擎单测 14/14。
- **D22 多条件节点（switch/if-else）+ 条件分支绑定 + 绑定表达式类型选择**（2026-09-17 迭代）：①新增 `switch` 节点（NodeTypes.Switch + SwitchNodeExecutor）——config.branches=[{id,label,binding}] 按顺序用 IExpressionEvaluator 求值，第一个真值分支命中：输出=输入透传+{result: 分支 id}，全部未命中 result="else"；调度器新增按 result 字符串匹配出边 condition 标记；验证器校验出边标记 ∈ 分支 id ∪ {else} 且不重复、至少一个分支。②前端 switch 节点用 **FlowGram 动态端口**（meta.useDynamicPort + 分支行渲染 [data-port-id] 锚点 + 分支数变化时 updateAllPorts），分支可增删/命名/选表达式类型；sanitizeBinding 抽出复用、fromEditorFormat/toEditorFormat 支持 branches 往返。③条件节点绑定模式表单精简（去掉 condition 字段名/必填/添加输入）并新增**分支绑定**：显示两条出边目标，「满足时走哪条」存 config.trueTarget（布局：满足时/不满足时固定顺序在左、绑定的节点在右）（目标节点 key），fromEditorFormat 据此写出边 condition 标记（trueTarget 随 id→key 重写、失效时回退端口标记），引擎零改动；④绑定行新增表达式类型选择（变量/JSONPath/插值/固定值，引擎 ExpressionEvaluator 已支持）；⑤验证——引擎单测 14/14、workflow-e2e **38/38**（WF-16 脚本条件、WF-17 多条件双分支）、前端 318/318、浏览器实测拖入 switch/动态端口/分支增删/条件分支绑定交换（2026-09-17）。
- **D23 知识库检索节点**（2026-09-18 迭代）：新增 `knowledgeSearch` 节点——引擎定义端口 `IWorkflowWikiSearchClient` + `WorkflowWikiSearchHit`（保持引擎零依赖），宿主 `WorkflowWikiSearchClient` 过滤出本团队知识库后转调 wiki 的 `IWikiSearchService`（按各库配置的 embedding 模型向量召回，未配置向量化/无命中的库静默跳过）。配置 `config.wikiIds`（数字数组）+ `config.topK`（每库召回条数，默认 5、上限 50），输入 `query`（必填），输出 `{query, count, hits:[{wikiId,documentId,documentName,chunkId,content,score}], contents:[string], text}`——text 为「【文档名】\n内容」空行拼接，供下游 aiChat 插值引用。保存草稿/发布/调试执行前经 `KnowledgeSearchWikiGuard` 校验 wikiIds 均属本团队（400 拒绝并回显无效 id），运行时端口按 `WorkflowExecutionContext.TeamId` 再过滤兜底；空 wikiIds 时节点失败提示「未配置知识库」。设计器：AI 能力分组节点、知识库多选（`getWikis`）+ topK 数字输入 + 检索问题绑定（四类表达式 + 上游变量提示）+ 只读输出声明；`sanitizeSettings` 白名单放行 `wikiIds`/`topK`（正整数去重、topK 1-50 截断）。验证：e2e WF-18a~i **47/47**、前端转换层 26/26、前端回归 321/321（2026-09-18）。
- **D24 知识库检索·单知识库 + 变量动态绑定**（2026-09-18 迭代）：一个节点只绑定**一个**知识库，静态选择与变量绑定合并为**同一个下拉输入框**（AutoComplete 分组列出「本团队知识库」与「上游变量」，纯数字视为静态选择写 `config.wikiId`，变量引用写输入绑定 `wikiId`）。执行器解析顺序：输入 `wikiId`（变量绑定，数字/数字字符串）→ `config.wikiId`（静态）→ 兼容旧版复数 `wikiIds`（输入/配置，数组或逗号分隔）；输入解析统一经 `ToJsonString()` 取原始文本（规避 JsonValue TryGetValue 严格类型匹配的坑），非空输入优先于静态配置，两者皆空节点失败。配套放宽 `WorkflowValidator`：空值 variable 绑定不再报「格式无效」（对齐客户端校验与运行时 required 语义），否则模板默认未绑定的节点无法保存草稿。动态 id 无法在保存期校验归属，由运行时端口按 TeamId 过滤兜底（越权 id 静默忽略）；静态 `config.wikiId` 保存期由 `KnowledgeSearchWikiGuard` 校验属本团队（兼容校验旧版 wikiIds）。验证：引擎单测 21/21（`KnowledgeSearchNodeTests`：单/复数优先级、解析格式、兜底失败、输出结构、空绑定校验）、e2e WF-18i + WF-19a~g **54/54**、前端 325/325、浏览器实测合并下拉分组渲染（2026-09-18）。
- **D25 连线交互约束：单出边 + 拖线切换 + 右键删除**（2026-09-18 迭代）：①普通节点（开始/AI 对话/脚本/插件/知识库检索）只允许一条输出连线，分支语义由条件/多条件节点承担——引擎 `WorkflowValidator` 与设计器 `validateEditorData` 双侧校验（超限报「只允许一条输出连线」）；②设计器 `onDragLineEnd` 钩子：普通节点拖出新连线后自动 `dispose` 旧出边（拖线即切换下游，FlowGram 原生 `resetLine` 支持拖已有线条端点重连，`canResetLine`/`canAddLine` 可按需扩展）；③画布右键菜单（React 事件委托在 `.wf-canvas`，FlowGram 节点包装层自带 `data-node-id`，线条经 `linesManager.getCloseInLineFromMousePos` 按坐标命中）：右键节点 → 删除节点（`node.dispose()`），右键连线 → 删除连线（`line.dispose()`），均可撤销；菜单用 fixed 定位 + 全局 mousedown/contextmenu 关闭。验证：引擎单测 23/23、ui vitest 27/27（单出边用例）、e2e 54/54、浏览器实测右键删除与拖线切换（2026-09-18）。
- **D26 团队工具 Tab + 插件选择器**（2026-09-18 迭代）：①「添加节点」面板改为两个 Tab——「节点类型」（原分组网格）与「团队工具」（`getTeamPlugins` 拉取团队可用插件，懒加载）；拖拽工具直接生成**已绑定该插件**的插件节点：拖拽负载携带 `{type:'plugin', pluginKey, title, description, outputs}`，`handleDrop` 合并进节点 data（settings.pluginKey + 标题/描述 + 按响应 schema 预填的输出参数）；②插件节点表单补上插件选择器（D19 遗留缺口关闭）：选择工具后自动按 `responseSchema` 覆盖输出参数（schema 为空则保留现输出，D13 语义）；③入参/出参 schema：`TeamPluginItem` 新增 `ParamsSchema`（静态/动态模板由请求类型 `Request` 反射生成，与 ResponseSchema 同法，custom 插件为 null）；输出映射收敛为 `utils.outputsFromPluginSchema`、输入映射为 `inputsFromPluginSchema`（fixed 空值绑定 + 类型/描述，剔除无名项），拖入与表单换选时同时自动填充输入与输出参数。④插件节点表单字段只读：输入/输出由插件 schema 自动生成，**不可增删字段**——输入行仅可设置取值方式（表达式类型/值/必填，`PluginBindingsSection`），输出为只读展示（`OutputsSection`）；字段名与描述以只读样式展示。验证：ui vitest 328/328、typecheck/lint 0 error、浏览器实测 Tab/拖拽/选择器（2026-09-18）。
- **D27 设计器真全屏 + 工具栏右下 + 抓手模式**（2026-09-18 迭代）：①流程应用设计分区默认分区为 design 但 URL 缺 `/design` 后缀，`AppLayout.FULLSCREEN_PATH` 匹配不上导致残留 24px 全局内边距——`AppWorkspace` 在 `validSection==='design' && section!=='design'` 时 `navigate(replace)` 到 `/design` 子路由，命中既有全屏规则（URL 可深链，其他分区/Agent 应用不受影响）；②缩放工具栏 `.wf-canvas-tools` 移到画布右下角（right/bottom 16px）；③新增**抓手（平移）模式**按钮：切换 FlowGram 编辑器状态 `EditorState.STATE_MOUSE_FRIENDLY_SELECT`（`playground.editorState.changeState(.id)` / `toDefaultState()`），开启后拖拽画布空白即平移（光标 grab/grabbing），再次点击恢复选择模式。验证：浏览器实测 padding=0、工具栏右下 16px、抓手拖拽平移 (250,162)px 且可关闭；ui vitest 328/328（2026-09-18）。
- **D28 问题分类节点（questionClassifier）**（2026-09-18 迭代）：新增 `questionClassifier` 节点——AI 模型把用户问题归入预定义分类之一，按命中分类路由分支。①契约：`config = { aiModelId, backgroundKnowledge?, historyCount?, classes:[{id,label}] }`，输入 `query`（必填）+ `history`（可选 `[{role,content}]` 数组），输出 = 输入透传 + `result`（分类 id）/ `className`（分类名）；②执行：`QuestionClassifierNodeExecutor` 组装「类型列表按序号 + 背景知识 + 只输出序号」系统提示词，经 `IAiChatClient` 调模型（history 按 `config.historyCount` 截取最近 N 条，默认 6、上限 50，深拷贝避免重挂载输入），输出解析三级兜底——序号数字 → 分类名包含匹配 → 默认第一个分类（流程继续，进度事件提示兜底）；③路由：调度器 `RouteOutgoingEdges` 对 questionClassifier 复用 switch 的 result 字符串匹配（分类 id 即出边 condition 标记，无 else）；④校验：`WorkflowValidator` 校验至少一个分类、分类名非空且不重复、出边标记 ∈ 分类 id 且不重复、允许多条出边（白名单）；缺模型/缺 query 不做保存期校验（与 aiChat 一致），执行期确定性失败挂起；⑤设计器：AI 能力分组节点模板，FlowGram **动态端口**（`useDynamicPort` + 分类行 `[data-port-id]` 锚点 + 数量变化 `updateAllPorts`，与 switch 同机制），模型选择（`getTeamGatewayModels`）+ 背景知识 + 聊天记录条数 + 用户问题绑定 + 可选历史消息绑定 + 分类增删编辑器；`sanitizeSettings` 放行 `backgroundKnowledge`/`historyCount`（0-50），`config.classes ↔ data.classes` 往返；⑥验证：引擎单测 38/38（`QuestionClassifierNodeTests` 15 例）、workflow-e2e **60/60**（WF-20a~f）、前端 336/336、typecheck/lint 0 error（2026-09-18）。
- **D29 HTTP 请求节点（http）**（2026-09-18 迭代）：新增 `http` 节点，发起自定义 HTTP 请求调用外部接口。①契约：`config = { method(GET/POST/PUT/DELETE/PATCH/HEAD), url, timeoutSeconds(1-300 默认 30), params/headers/formEntries:[{name,value}], bodyType(none/json/form/text，缺省有 body 视为 json), body, auth:{type(none/bearer/basic/apiKey), token, username, password, headerName(默认 X-API-Key), headerValue}, errorCapture, extract:[{name,path(JsonPath),fieldType}] }`，无输入绑定——URL/参数值/请求头值/请求体/鉴权值均支持 `{引用}` 插值（URL 走 IExpressionEvaluator 严格解析，引用缺失节点失败；其余安静解析为空串，容忍分支未执行）；②输出固定结构 `{ statusCode, rawResponse(JSON 可解析为对象否则为文本), hasError, errorMessage, ...extract 字段 }`，提取未命中置 null 保持结构稳定；③失败语义：网络错误/超时（独立 linked CTS 控制超时，用户取消原样抛出）/状态码 ≥400 默认节点失败挂起；`errorCapture=true` 时节点完成并输出 hasError=true（响应体仍可用于提取）；响应体读取上限 5MB；④执行器内置静态 `HttpClient`（SocketsHttpHandler，连接池复用），构造函数保留 `HttpMessageHandler` 可选参数供单测注入桩（`StubHttpHandler`）；⑤校验：`WorkflowValidator` 调 `HttpRequestNodeExecutor.ValidateDefinition`——方法/超时/Body 类型/鉴权类型白名单、url 必填、提取字段名非空唯一且 JsonPath 可解析、配置中 `{引用}` 必须指向 sys/system/input 或上游节点（与输入绑定同规则）；⑥设计器：集成分组节点，表单在 `HttpNodeForm.tsx`（共享小部件抽到 `node-form-widgets.tsx`/`node-form-shared.ts`）——方法+地址（AutoComplete 选中变量追加 `{引用}` 模板）、超时、Params/Body/Headers 三 Tab（Body 按 bodyType 切换 JSON/文本多行输入或表单键值对）、鉴权、报错捕获 Switch、字段提取编辑与固定输出展示；**cURL 导入**（`curl.ts` shell 词法切分 + `-X/-H/-d/-u/--get/-I` 解析，Bearer/Basic 提升为鉴权配置，k=v 多段转表单字段）；`sanitizeSettings` 白名单放行 http 全部键（方法大写、KV 剔除无名项、auth 按类型留字段、extract 去重）；`nodeDataFromTemplate` settings 改为深拷贝（模板含嵌套数组，避免多节点共享引用）；⑦出站安全：节点可请求任意 http/https 地址（与插件出站同级别），SSRF/内网访问控制由部署侧网络策略承担（见 §6）。验证：引擎单测 50/50（`HttpRequestNodeTests` 12 例：请求组装/鉴权/请求体/提取/报错捕获/超时/校验）、workflow-e2e **70/70**（WF-21a~j，脚本内起本地桩服务回环验证）、前端转换层 41/41（含 cURL 解析）、typecheck/lint 0 error（2026-09-18）。
- **D30 发布应用对话（会话化 + sys 系统变量）**（2026-09-18 迭代）：流程应用复用 Agent 的 AG-UI 对话栈，一轮对话 = 一次已发布流程执行。①引擎：`WorkflowInstance.SystemContext`（调用方注入的 sys.* 附加变量，随实例持久化、断点恢复可用），`BuildVariableScope` 在内置项（instanceId/workflowId/workflowName/startedAt）之外新增 `sys.currentTime`（实例启动时间，格式化 `yyyy-MM-dd HH:mm:ss`）并合并 SystemContext；`StartAsync/StartWithDefinitionAsync` 增加 `systemContext` 参数。②AI 层：`AppAgentFactory` 按 `app.AppType` 分流——Workflow 应用构造 `WorkflowAppChatClient`（`IChatClient` 适配器：取消息列表最后一条用户消息为 query，经端口接口执行流程，回复以单帧流式返回；执行失败转可见错误文本），会话历史提供者/Redis 热态/落库管线与 Agent 完全复用；端口 `IWorkflowAppChatInvoker` 定义在 MoAI.AI.Core，实现在 Workflow.Core `WorkflowAppChatInvoker`（依赖方向 Workflow.Core→AI.Core 单向）——校验已发布 → 注入 sys 上下文（userId/appId/conversationId/messageId/history，history 取落库最近 20 条 user/assistant，形状 `[{role,content}]` 可直连 aiChat 节点）→ `query` 启动流程 → 从结束节点输出提取回复（`reply/answer/output/text/result` 字符串字段优先 → 唯一字符串属性 → 整体 JSON 序列化）。③会话与调试：`CreateAppSessionCommandHandler` 放开 Workflow 类型（公开/成员/发布规则与 Agent 一致）；调试执行注入调试 sys 上下文（userId/appId 为真实值，conversationId/messageId/history 为空），设计器引用可解析。④前端：`AppWorkspace`/`TeamApps` 对已发布流程应用开放「进入对话」（复用 `AppChat` 页，零改动）；设计器「系统设置」面板新增「系统变量」只读分区（使用者 ID/应用 ID/当前对话 ID/AI 回复的 ID/历史记录/当前时间），变量提示补全 `sys.*` 引用。验证：引擎单测 53/53（`WorkflowSystemContextTests` 3 例）、workflow-e2e **82/82**（WF-22a~l：会话门禁/失败可见/发布回显流程/两轮 sys 变量与 history 累积/消息落库/运行历史）、AP 121/121、PT 46/46 回归（2026-09-18）。


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

- fork/forEach/dataProcess 节点未实现（前端枚举与常量结构已预留）；知识库检索已落地为 `knowledgeSearch` 节点（见 D23）。
- 调试执行为同步请求（长流程受 HTTP 超时约束）；SSE 事件流式与实例事件持久化为二期。
- 正式执行入口（会话化对话）已开放（见 D30）：成员对已发布流程应用创建会话经 AG-UI 端点对话；回复为流程整体执行完一次性返回（非逐字生成），对外部访问点（external）的对话开放暂缓；回复提取约定 `reply` 字段优先（见 D30）。
- 设计器多人实时协同（Yjs）未纳入本期。
- 调试执行为「隐式保存草稿再跑」，运行历史中调试实例的输入为面板当前值；正式执行（对话）实例的输入为 `{query: 用户消息}`。
- HTTP 请求节点可请求任意 http/https 地址（流程平台标准能力，与插件出站同级别），内网/元数据地址的 SSRF 防护由部署侧网络策略承担；响应体读取上限 5MB、超时 1-300 秒（见 D29）。
