# 知识图谱重构设计方案（2026-11-08）

> 关联：[SDD](../../knowledgegraph/sdd.md) ｜ [BDD](../../knowledgegraph/bdd.md) ｜ [TDD](../../knowledgegraph/tdd.md) ｜ [SOP](../../knowledgegraph/sop.md) ｜ 前置调研：LightRAG（本地克隆 `F:\sourcecode\LightRAG`，MIT）+ RAGFlow（Apache-2.0）+ Neo4j Bloom/Browser + Memgraph Lab + FalkorDB Browser 交互范式
> 状态：**待评审**。通过后按分期落地，决策并入 [sdd.md §8](../../knowledgegraph/sdd.md)（自 D25 起），场景并入 [bdd.md](../../knowledgegraph/bdd.md)（自 @KG-S35 / @KX-16 起）。

## 1. 背景与诊断

对 LightRAG 源码级分析（插入管线/存储抽象/并发/服务层）与社区产品交互调研后，判定 KG 模块的问题是**三个定位错位**，而非功能缺失：

| # | 错位 | 现状（代码事实） | 目标 |
|---|---|---|---|
| 1 | 图是「死」的 | 导入为覆盖式 upsert（`KnowledgeGraphDataImportService` 整体覆盖语义）；AI 导入同步长请求（`import-file` 截断 1.2 万字符→对话模型抽取→校验→写图，一次到底）；无导入状态机、无证据链 | 图是持续演进的知识管线：源→抽取→归并→入图，可重跑、可追溯、可治理 |
| 2 | 图是「孤」的 | 召回测试（`recall-test`）、Text2Cypher（`kg_cypher_query` 插件）、画布（`canvas`）三个能力互不相通，各自孤立页面/工具 | 检索即可视化：召回/查询结果直接在画布高亮子图，探索-验证-调试一体 |
| 3 | 图是「表单」的 | 画布 `KnowledgeGraphCanvas.tsx`（923 行）：d3-force 单布局同步 280 ticks 无动画；展开只增不减（上限固定 100）；详情抽屉纯只读；过滤仅顶部 Tag 单选；搜索=整图重载 | 探索工作台：搜索定位/图例多选/双击展开收起/侧栏编辑/实体合并（LightRAG WebUI 已开源同款，MIT） |

对照基线：LightRAG 插入管线的**证据链幂等合并**（`lightrag/operate.py:2429` `_merge_nodes_then_upsert`：source_id 证据追加 + weight 防双计 + description 跨代去重）与**两级召回**（向量找种子→图邻接 degree/weight 排序扩展→token 预算组装，`operate.py:6075` `_build_query_context`）是数据质量与召回质量的核心机制；前端六件套工具条/图例/详情侧栏/合并对话框在 `lightrag_webui/src/components/graph/` 有完整 MIT 实现。

## 2. 目标与非目标

**目标**
1. 导入幂等可重跑：重复导入/部分失败重试不产生脏数据，全程状态可见可断点。
2. 召回质量升级：两级召回 + 度数排序 + 预算组装，对齐 GraphRAG 主流实现。
3. 画布成为探索工作台：P0 补齐六件套与展开/收起，P1 治理闭环（合并）与检索联动。
4. 治理闭环：图上实体合并，衔接外部导入已有的疑似重复检测（≥0.85 `duplicateSuspects`）。

**非目标（明确不做）**
- 不做内嵌图数据库档（LightRAG 为 NetworkX 文件档付出的单写者契约/双通道 fence 复杂度是反面教材，我们外挂 Memgraph 不背此债）。
- 不做社区检测/层次摘要（SP-A 二期展望项，待 MAGE Leiden 真需求）。
- 不做多图数据库物理分库（维持 D20 单库 + kgId 隔离）。
- 外部接口 `/api/teamres/knowledge-graph` 既有契约不变（`import` 保持同步 + validateOnly 语义），仅增量。

## 3. 使用方式重构：按生命周期重排

```
建图 ──→ 治理 ──→ 探索 ──→ 消费 ──→ 开放
 │        │        │        │        │
 │        │        │        │        └ MCP/外部API/资源key（现状保持）
 │        │        │        └ 应用绑定/对话工具/工作流 kgSearch（现状保持）
 │        │        └ ★画布探索工作台（F0/F1 主战场）
 │        └ ★实体合并/重复处理（B6+F1）
 └ ★导入管线化：异步任务+状态机+进度+重跑（B1/B2）
```

信息架构不变（详情 section：图览/导入/召回测试/MCP/模型/设置），重排的是每个 section 内部的形态：导入页从「两个表单 Tab」变「发起 + 任务历史」；召回测试页增加「在画布查看」；图览页变工作台。

## 4. 后端改造（B1~B7）

### B1 证据链与幂等合并（数据质量地基，最优先）

**现状**：`KgNode {id, kgId, entityTypeId, name, description, key?, propsJson}` / `KG_REL {id, kgId, relationTypeId}`（sdd §3.2）。外部 import upsert 为整体覆盖；AI/JSON 导入无来源概念；同三元组平行边靠 import 幂等跳过，人工与混源路径无防护。

**改造**（参考 `operate.py:2429` 合并语义，适配到我们的类型化 schema）：
- 节点/边增可选属性 `srcJson`（JSON 字符串，沿用 D24 propsJson 单属性模式，**无 schema 迁移**）：数组元素 `{batchId, docName, sourceKey?}`，上限 20 条 FIFO（对齐 LightRAG `apply_source_ids_limit`）。
- 合并策略 `MergeKnowledgeGraphNodes`（Core 内服务方法）：**匹配**=（类型+名称）或 key 命中即合并（与外部 import 匹配规则同构）；**属性**=description 拼接去重（跨代去重后追加，超 2000 字符截断标注，不调 LLM 摘要——我们图谱 description 量级小，先不做 LLM 摘要，留 P2 开关）；propsJson 深合并（同键 target 优先）；entityTypeId 保留 target；srcJson 并集去重。
- **边防双计**：同三元组已存在则仅追加 srcJson 证据（不新建边）；新证据条目 = 新 batchId 未出现过。
- 写入点收口：`KnowledgeGraphDataImportService`（外部 import/JSON 导入共用管线）与 AI 导入解析落库统一走合并语义；手工 CRUD 不动（手工编辑即用户意志）。
- 向量联动：合并后节点发 `kg.node.embedding` delta（Consumer 本就幂等 upsert 读最新状态，天然兼容）。

**验收**：同一 JSON 导入两次 → 节点/边数量不变、srcJson 证据合并；AI 导入同文档重跑 → 不重复建边。

### B2 导入管线状态机（AI 导入异步化）

**现状**：`ImportKnowledgeGraphFromFileCommandHandler` 同步全流程；`import-json` 瞬时同步（量小时可接受）。

**改造**（参考 LightRAG doc_status 状态机 + 「通知可丢、状态不可丢」）：
- 新表 `kg_import_job`：`id / kg_id / team_id / type(file|json) / status(Pending|Extracting|Parsing|Merging|Done|PartialFailed|Failed|Cancelled) / total_rows / done_rows / error_json / result_json / 审计`。PG 为真相源，无 MQ 依赖（进度走轮询，复用 app-e2e 已有的轮询断言模式）。
- `POST /{id}/import-file` **契约变更**：返回 `{jobId}`（不再同步返回统计）；新增 `GET /{id}/import-jobs`（分页）与 `GET /{id}/import-jobs/{jobId}`（状态+进度+逐段错误）、`POST /{id}/import-jobs/{jobId}/retry`（失败重跑，依赖 B1 幂等）、`POST /{id}/import-jobs/{jobId}/cancel`（取消未完成段）。Hangfire 执行（`BackgroundJob.Enqueue`，项目内已有惯例）；job 执行内逐段更新 `done_rows`（提取→抽取→校验→合并四段落进度）。
- `import-json`：小载荷保持同步（页面表单体感不变）；>200 节点自动转 job（同表同状态机）。
- 外部 `/import` 不动（26 端点契约保持，其行级报告语义本就优于 job 模型）。

**验收**：导入大文档期间可查询进度；模拟抽取失败 → job=Failed 带错误段；retry 后 Done 且无重复数据（B1）。

### B3 召回管线升级（两级召回 + 预算组装）

**现状**：`GraphSearchService`：向量召回 top 实体 → 一跳邻接（邻居 ≤10 固定截断）→ `GraphSearchTextHelper` 文本化。无度数排序、无关键词双路、无预算控制。

**改造**（参考 `operate.py:6075`，SP-A 二期对齐）：
- 邻接扩展改 **度数排序**：Cypher 一跳查询按 `size((n)--())` DESC 取 top-K（K 默认 10，`/search` 与 `recall-test` 增可选 `neighborTopK` 1~30）；返回 hits 增 `degree` 字段。
- **上下文预算**：组装文本按字符预算裁剪（对话工具 16KB payload 预算已存在，`GraphAppToolProvider`；将裁剪逻辑下沉到 `GraphSearchTextHelper`，命中片段按 (score, degree) 排序后依预算截断，尾部标注 truncated）。
- `recall-test` 增可选 `isKeywordExtract=true`：对话模型先抽 high/low 关键词（JSON 强约束 + 容错解析，复用 `KnowledgeGraphImportParser` 的 JSON 容错思路），两路查询向量取并集种子（P1 实现，接口字段先留）。
- 边向量化（关系语义检索）不在本期（SP-A 二期原项）。

**验收**：同 query 下 hits 按 score+degree 排序可断言；预算截断在桩模型 E2E 可断言 truncated 标记。

### B4 图 CRUD 不变量守护（小改）

参考 LightRAG graph_routes 的校验层守护核心不变量：
- 合并（B6）时：source 节点的边全部重挂到 target，**合并后不得丢边**（重挂后同三元组去重保留一条，srcJson 并集）；带 key 的 source 节点合并后 key 随节点消失，外部 `keys/list` 自然出差集——文档化为同步语义而非错误。
- 删除节点：已有 DETACH 级联；补「该节点有 ≥N 条边时要求 `confirm=true`」（防误删枢纽节点，N=20）。
- 边起止约束、类型引用拦截已有，不动。

### B5 抽取模型角色化默认（小改）

参考 LightRAG ROLES（default/extract/keyword/query 独立绑定三级回退）：
- `kg` 表增可空 `extract_model_id`（对话模型，AI 导入默认抽取模型）；请求级 `aiModelId` > 图谱默认 > 400 要求显式指定。recall-test 的回答模型沿用请求级（现状）。
- 设置页「向量化模型」旁增「抽取模型」选择（Admin+）；模型校验复用 `QueryKnowledgeGraphModelOptionsCommandHandler` 分桶（conversationModels）。

### B6 实体合并命令（治理闭环）

- `POST /{id}/nodes/merge`：`{targetId, sourceIds[], confirm}`，仅托管图 Admin+；语义=B1 合并策略 + 边重挂（B4 不变量）+ target 向量 delta 重发；sourceIds 含不存在/即 target 时 400；`confirm` 缺省且影响边数 ≥ 20 时 409 要求确认（对齐 B4 删除防护阈值）。
- 前端入口两处：画布多选 → 合并对话框（属性差异对比表：target 列/各 source 列，冲突项勾选保留方）；导入页 `duplicateSuspects` 列表「去合并」跳画布并预选。
- 与外部 by-key 闭环的关系：合并是托管侧治理动作，外部同步方通过 keys/list 差集感知（B4 已文档化），不新增外部端点。

### B7 子图与邻接查询增强（画布后端支撑）

- `POST /{id}/canvas` 增可选 `depth`（1~2，默认 1；全局 limit≤500 封顶不变）——局部图模式的 N 跳数据面。
- `GET /{id}/nodes/{nodeId}/neighbors` 响应节点项增 `degree`、`expandedAble`（有无未加载邻居）；支持 `?exclude=ids` 逗号列表（收起语义由前端维护已展开集合，后端无状态，对齐 LightRAG 无状态协议行为）。
- 新增 `POST /{id}/text2cypher`：`{question}` → 复用 `KgCypherAccessService`（schema 自描述 + 只读守卫 + `$kgId` 归属校验均为现成能力）+ 对话模型生成 Cypher → 执行 → 返回 `{cypher, nodes, edges, truncated}`；执行失败/非只读拒绝时返回可读错误与生成原文。Member 可用（只读面）。防炸：结果节点封顶 500（同 canvas）。

## 5. 前端改造

### 5.0 引擎决策（D25，先拍板）

**推荐：画布切回 `@antv/G6` 5.1.x（MIT），移除 `@xyflow/react`。**

历史与现状差异论证：
- v3.1 从 G6 切走的原因（sdd §7）：① G6 v5 点击命中判定 `targetType` 坑（v2~v2.7 点击展开实际失效）；② 需要 React 组件节点做卡片渲染。
- 现在切回的理由：① 当时是**编辑器型**需求（Handle 拖线建边、右键增删），React Flow 是正确选择；本次是**探索型**需求（大图渲染、邻接高亮、布局动画、图例、鱼眼），G6 内置 behaviors/plugins 直接覆盖，React Flow 全要手写；② G6 5.1 已被 RAGFlow 生产实证（combo 布局 + hover-activate + tooltip + 调色板）；③ React 19 下命令式 `useRef/useEffect` 创建 Graph 实例即可（Graphin/@ant-design/graphs 不可用，不采用）；④ **`@xyflow/react` 当前全仓只有 KG 画布在用**（已核实 `ui/src` 无其他引用），切换后可整体移除，无双库共存成本；⑤ 当年 `targetType` 坑属 API 用法问题（v5 事件契约），已有 RAGFlow 源码可对照。
- 编辑能力保留策略：G6 下「右键新建/删除、连线建关系」改用 G6 behavior（`create-edge` 内置拖线）+ 自定义右键菜单；节点卡片渲染用 G6 v5 React node 扩展（`@antv/g6` 支持自定义 render，或退化用内置 badge/标签组合——以 RAGFlow KG 页实现为参照）。
- 备选方案 B（不推荐）：留在 React Flow 手写六件套 + 布局动画 + 邻接高亮，估 +4~6 人日且上限低。
- 风险缓释：画布路由级懒加载（`React.lazy`）控制 G6 体积；切换在独立 PR，画布旧实现保留一个 tag 可回退。

### 5.1 P0：画布可用性地基（引擎切换 + 六件套）

`KnowledgeGraphCanvas.tsx` 重写为 `ui/src/pages/knowledgegraph/canvas/` 目录组件群（对齐 LightRAG `lightrag_webui/src/components/graph/` 组织）：

| 组件 | 职责 | 参考 |
|---|---|---|
| `GraphCanvas.tsx` | G6 实例生命周期（useRef 命令式）、数据→G6 model 映射、类型调色板着色（类型色板沿用现 schema 色板字段） | RAGFlow G6 配置 |
| `GraphToolbar.tsx` | 搜索框（实体搜索模式）、布局切换（force/circular/grid + 过渡动画）、缩放/适配/全屏、显示设置抽屉（标签显隐/边宽映射/字体缩放） | LightRAG GraphControl 六件套 |
| `GraphLegendPanel.tsx` | 左下图例：类型+计数+**多选**过滤（内存过滤不重载，替代现顶部 Tag 单选+服务端重载） | LightRAG GraphLabels/Legend |
| `NodeDetailSidebar.tsx` | 点节点右侧栏：属性 + **关联边列表**（对端可点跳转/隐藏）+ 邻居 chips + 编辑入口（跳现有表单）+ 合并入口（P1）；点边同栏显示关系详情 | LightRAG PropertiesView |
| `useExpandCollapse.ts` | **双击展开/收起**：已展开集合跟踪，展开调 neighbors（B7 degree 增强），收起移除该子树未被他节点引用的节点；上限可调（50/100/200） | Neo4j Browser 范式 |
| `GraphSearch.tsx` | 搜索→客户端定位居中+高亮脉冲（不重载）；P1 加自然语言模式 | LightRAG GraphSearch FocusOnNode |

同步改造：`KnowledgeGraphDetail.tsx` 布局适配（侧栏与画布并存）；编辑态交互（右键菜单、拖线建边）迁移；i18n zh/en 全量；design-system token 合规（危险操作 Popconfirm）。

### 5.2 P1：治理与检索联动

1. **合并对话框** `MergeNodesModal.tsx`：画布多选（框选/ctrl 点选）→ 差异对比 → 调 B6 端点；导入页 duplicateSuspects「去合并」深链 `canvas?merge=ids`。
2. **召回联动画布**：`KnowledgeGraphRecallTest` 结果面板增「在画布查看」→ `/canvas?highlight=nodeIds`，画布读参拉取子图高亮命中路径（B3 的 degree/score 数据顺带用于着色深浅）。
3. **Text2Cypher 搜索模式**：工具栏搜索框双模式切换（实体/自然语言），自然语言模式调 B7 端点，展示生成 Cypher（可复制）+ 结果子图高亮；失败展示守卫原因（只读拒绝/图库不可达）。
4. **画布样式规则**（FalkorDB/Memgraph Lab 思路）：按类型配色板 + caption 显示属性选择 + 边宽映射（度数/证据数），存图谱 settings（Admin 配置全图生效，Member 只读跟随）。
5. **局部图模式**：深度滑杆（1~2 跳，B7 depth 参数）+ 「聚焦此节点重载画布」。

### 5.3 P2：差异化（可选，另行立项）

- 知识编译多形态视图（同图谱 → 思维导图/表格视图，RAGFlow 思路，`@antv/gpt-vis` 已在依赖内）。
- MAGE Leiden 社区检测 + 社区着色/折叠（需 Memgraph MAGE 可用性探测）。
- description 合并的 LLM 摘要开关（B1 留的口子）。

## 6. 数据与 API 变更总表

| 类别 | 变更 | 兼容性 |
|---|---|---|
| PG 新表 | `kg_import_job` | 新增，无迁移风险 |
| PG 列 | `kg.extract_model_id`（可空） | 新增列，asserts SQL |
| 图库属性 | `KgNode.srcJson` / `KG_REL.srcJson`（可选字符串） | 无 schema 迁移（属性不存在即无证据，读侧容错） |
| API 破坏性 | `POST /{id}/import-file` 返回体 统计→`{jobId}` | **不兼容**：前端与 `kg-e2e.mjs` 同 PR 内切换；外部接口不受影响（import-file 非外部端点） |
| API 新增 | `GET /{id}/import-jobs`、`GET /{id}/import-jobs/{jobId}`、`POST .../retry`、`POST .../cancel`、`POST /{id}/nodes/merge`、`POST /{id}/text2cypher`、canvas `depth`、neighbors `degree/exclude`、search/recall `neighborTopK` | 均可选参数/新端点 |
| 移除依赖 | `@xyflow/react`、`d3-force`（确认无他用后） | 画布独立 PR |

## 7. 分期计划与验收

| 期 | 后端 | 前端 | 验收（全部需 `dotnet build` 0 error + `npm run typecheck && lint && test` 绿） |
|---|---|---|---|
| **P0a 数据地基** | B1 证据链合并、B2 导入状态机、B5 抽取模型默认 | 导入页改「发起+任务历史」（轮询进度、失败重跑按钮） | 扩展 `local-dev/kg-e2e.mjs`（需 Memgraph）：B1 幂等重导两遍计数不变、job 状态机全路径、retry 幂等；场景 @KG-S35 起（导入管线 Feature） |
| **P0b 画布地基**（可与 P0a 并行） | B7 depth/degree/exclude | 5.0 引擎切换 + 5.1 六件套 + 双击展开收起 | 新增 `local-dev/kg-canvas-e2e.mjs`：depth 参数/degree 回显/exclude/上限截断（API 面）；视觉交互 @manual + `__tests__` 组件测试（图例多选过滤、展开收起状态） |
| **P1 召回与治理** | B3 召回升级、B6 合并、B4 不变量、B7 text2cypher | 合并对话框、召回联动、Text2Cypher 模式、样式规则、局部图 | 扩展 kg-canvas-e2e：merge 全链路（边不丢/向量 delta/key 差集）、text2cypher 只读守卫与 500 封顶；recall 排序断言进 `kg-search-e2e.mjs`；场景续编 + @KX-16（keys/list 差集文档化场景） |
| **P2 差异化** | 视需求 | 视需求 | 另行立项 |

每期完成即按 DOC-STANDARD 同步四件套：bdd 场景 → tdd 映射执行 → sdd 决策/已知问题 → sop 排障表（导入 job 失败排查、合并操作守则）。

## 8. 决策记录（待评审，D25~D31）

- **D25** 画布引擎回归 G6 5.1.x、移除 @xyflow/react（论证见 §5.0；当年切走是编辑器需求，本次是探索需求，且 xyflow 无其他消费方）。
- **D26** 证据链存 `srcJson` 单属性（沿用 D24 模式，上限 20 条 FIFO；不做 LLM 摘要，留 P2 开关）。
- **D27** AI 导入异步化：import-file 契约变更为返回 jobId（单仓一次性切换，不留 sync 兼容模式；外部 /import 不动）。
- **D28** 召回两级扩展：度数排序邻居 + 预算裁剪下沉 GraphSearchTextHelper；关键词双路 P1 落地（接口字段先留）。
- **D29** 合并语义：target 保留 + props 深合并（冲突 target 优先）+ 边重挂同三元组去重 + srcJson 并集 + 向量重发；key 随 source 消失由 keys/list 差集表达。
- **D30** 抽取模型三级回退：请求 aiModelId > kg.extract_model_id > 400。
- **D31** Text2Cypher 画布化走独立只读端点（复用 KgCypherAccessService 全套守卫），Member 可用。

## 9. 风险与回滚

| 风险 | 概率/影响 | 缓释 |
|---|---|---|
| G6 二次踩坑（v3.1 历史阴影） | 中/中 | RAGFlow 源码对照（Apache-2.0）；画布独立 PR + 旧实现 tag 可回退；先做 spike（1 天）验证：React19 集成、自定义节点、事件契约、1000 节点性能，spike 不过则降级备选方案 B |
| import-file 契约变更破坏调用方 | 低/中 | 全仓唯一消费方是前端与 e2e（已核实非外部端点），同 PR 切换 |
| 合并操作不可逆 | 中/高 | merge 前端强确认（差异对比+影响边数）；后端 `confirm` 二段式；软删不做（图库无软删语义，文档明示不可逆，建议大合并前导出 JSON） |
| 异步 job 与 Hangfire 依赖 | 低/低 | 项目已全面使用 Hangfire；job 表即真相源，Hangfire 丢任务可由「Pending 超时扫描」兜底重入 |
| E2E 环境 Memgraph 缺失 | 中/低 | 沿用现有 kg-* e2e 门控惯例（图库不可达 FAIL 标注）；B2 状态机单测（tests/MoAI.KnowledgeGraph.Tests）不依赖图库 |

## 10. 工作量估算（粗）

P0a 后端 5~6 人日 + 导入页前端 2；P0b 画布 6~8（含 spike 1）；P1 后端 4~5 + 前端 5~6；合计约 **22~27 人日**。P2 另计。

## 11. 参考实现锚点

- LightRAG 本地克隆：合并语义 `lightrag/operate.py:2429`；上下文组装 `:6075`；doc 状态机 `lightrag/base.py:1317`；图 API 校验 `lightrag/api/routers/graph_routes.py`；WebUI 组件 `lightrag_webui/src/components/graph/`。
- RAGFlow（GitHub）：KG 页 G6 配置 `web/src/pages/dataset/knowledge-graph`（调色板/hover-activate/combo 布局）。
- 本仓现状锚点：`src/knowledgegraph/MoAI.KnowledgeGraph.Core/Services/{KnowledgeGraphDataImportService,GraphSearchService,KgCypherAccessService,CypherKnowledgeGraphStore}.cs`；`Handlers/ImportKnowledgeGraph{FromFile,Json}CommandHandler.cs`；`ui/src/pages/knowledgegraph/KnowledgeGraphCanvas.tsx`（923 行，重写对象）。
