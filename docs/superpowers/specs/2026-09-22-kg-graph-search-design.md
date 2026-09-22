# 设计：知识图谱图检索消费层 SP-A 一期（实体向量化 + 应用绑定 + 对话工具 + 工作流节点）

> 日期：2026-09-22 ｜ 状态：已评审通过（用户确认 SP-A 方向与四项判断） ｜ 后续：writing-plans 出实现计划
> 关联：Text2Cypher 插件（specs/2026-09-21-kg-text2cypher-plugin-design.md，已落地）；KG SDD v2.8 规划的「应用绑定 graph_ids 与图检索」本设计落地其一期

## 1. 背景与动机

用户反馈知识图谱「只进不出、用面太窄」，六点诉求中本设计消解两条：**Agent 应用绑定多个知识图谱供 AI 搜索、流程应用支持知识图谱节点**（消费形态），并补齐**相似度/向量检索**（检索姿势）。策略：镜像 wiki（向量知识库）已验证的全套模式——向量化基建、应用绑定、工具注入、工作流节点三件套——把「照抄骨架、换图血肉」作为第一原则，GraphRAG local search 做轻量版（不上社区摘要，2026-09 调研结论）。

### 仓库惯例参照（已核实，2026-09-22 @21377123）

| 环节 | wiki 现状 | KG 镜像 |
|---|---|---|
| 向量存储 | `__wiki_{id}` 动态集合表（CommunityToolkit.VectorData.PgVector，HNSW+Cosine，维度取 `Wikis.EmbeddingDimensions`） | `__kg_{id}`，记录换 KgId/NodeId |
| Embedding 渠道 | `IEmbeddingGeneratorProvider` + `ResolveModelAsync`（join ai_model+ai_channel，团队授权校验） | 同款解析（KG 内镜像实现，模块不互相引用） |
| 增量向量化 | WorkerTask（活跃唯一约束）+ MQ Consumer 异步 | 同款，消息带增量 delta |
| 应用绑定 | `AppAgentConfigEntity.WikiIds` JSON 数组：保存校验→发布快照→`AppAgentFactory` 解析 | `GraphIds` 全套对齐 |
| 对话注入 | **纯工具**：`WikiAppToolProvider : IAppToolProvider` 暴露 `search_knowledge_base` | `GraphAppToolProvider` 暴露 `search_knowledge_graph` |
| 工作流节点 | `knowledgeSearch`：NodeTypes/Executor/Client 守卫三件套 + NodeForm 面板 | `kgSearch` 三件套镜像 |
| 已知缺口 | 删文档/删库**不清理向量** | KG 在删除链路显式清理（不复刻缺口） |

## 2. 目标与非目标

**目标（一期）**

1. 托管图实体（节点 name+description）向量化：异步任务、增量 upsert/delete、删图清库
2. `GraphSearchService`：问题 → 查询向量 → 各图 topK 实体 → 一跳关系扩展 → 子图文本化
3. Agent 应用绑定 `GraphIds`（多选、保存校验团队归属、发布快照）→ 对话暴露 `search_knowledge_graph` 工具
4. 工作流 `kgSearch` 节点（无 LLM 在环）：`{graphId, topK}` 配置、`query` 输入、`{hits[], contents[], text}` 输出
5. 团队级检索 API（`POST /knowledge-graph/{id}/search`）供 E2E 与后续召回测试 UI

**非目标（二期）**：关系/边向量化、rerank 及与 wiki 召回融合、召回测试 UI、citation 子图前端展示、接入图检索（外部库无向量基建，继续由 Text2Cypher 插件覆盖）、对话记忆图化。

## 3. 总体架构与数据流

```
节点 CRUD/批量导入/AI 导入完成
   └→ MQ delta 消息 {kgId, upsertNodeIds[], deleteNodeIds[]}（Maomi.MQ，RouterKey kg.node.embedding）
        └→ Consumer 认领（[Consumer(Qos=1)]，模块程序集扫描自动注册）
             └→ 逐节点读 PG KgNode → IEmbeddingGeneratorProvider 向量化 name+description
                  └→ __kg_{kgId} 集合：按 NodeId 先删后插；deleteNodeIds 直接删
   说明：wiki 的 WorkerTask（活跃唯一约束）用于「手动触发、需 409 防重」的文档向量化；KG 的节点级
   向量化由 CRUD 自动触发，任务冲突 409 会打断用户操作、活跃时跳过入队会丢 delta，故一期不建
   WorkerTask，依赖 MQ 重投 + 消费幂等（upsert 读 PG 最新状态，多次 delta 自然合并）；
   「全量重建 + 进度可见」留二期。

Agent 对话：模型 call_tool search_knowledge_graph({query})
   └→ GraphAppToolProvider（绑定 GraphIds 非空才注册）
        └→ GraphSearchService.SearchAsync(graphIds, query, topK, minScore)
             ├→ 每图解析 embedding 模型（未配置/渠道不可用 → 该图跳过，可读提示）
             ├→ 查询向量 → 集合 SearchAsync top 实体（minScore 过滤）
             ├→ CypherKnowledgeGraphStore 一跳邻接（复用，邻居数截断）
             └→ hits[{nodeId,name,description,entityTypeName,score,neighbors[]}] + text 文本化

工作流 kgSearch 节点：{query} → 团队守卫 → GraphSearchService → {query,count,hits,contents,text}
```

## 4. 向量化基建

- **存储**：`PgVectorKgEmbeddingVectorStore` 镜像 `PgVectorWikiEmbeddingVectorStore`；记录 `KgEmbeddingVectorRecord(Key=Guid, KgId, NodeId(string), EntityTypeId(long), Name, Content, Embedding)`；`__kg_{id}` 表、`DistanceFunction.CosineSimilarity` + `IndexKind.Hnsw`，维度取图谱配置（1-2000）
- **模型配置**：`KnowledgeGraphEntity` 新增 `EmbeddingModelId(Guid?)`、`EmbeddingDimensions(int)`（1-2000，默认 1024，对齐 wiki 两列口径；ai_model 主键为 Guid）；图谱设置页配置；未配置 → 图谱不参与检索
- **触发点**：`CreateNode`/`UpdateNode`/`DeleteNode` Handler、批量导入（UNWIND 批后）、AI 导入完成后——统一发 delta 消息；`DeleteKnowledgeGraph` → 删除 `__kg_{id}` 集合整表（修 wiki 缺口）；`UpdateNode` 若 name/description 未变则跳过向量化（消息里带内容哈希比对可后续优化，一期直接重嵌，量级可接受）
- **失败语义**：消费失败走 MQ 重投与死信（对齐 wiki），应用层不重试；向量缺失的图谱检索降级为空结果+提示，不报错

## 5. 检索服务（GraphSearchService，KG Core）

```csharp
Task<GraphSearchResult> SearchAsync(IReadOnlyList<long> graphIds, string query, int topK = 5, double? minScore = null, CancellationToken ct)
// GraphSearchResult { List<GraphSearchHit> Hits, List<string> Contents, string Text, List<string> SkippedHints }
// GraphSearchHit { long KgId, string NodeId, string Name, string Description, long EntityTypeId, string EntityTypeName?, double Score, List<GraphNeighbor> Neighbors }
// GraphNeighbor { string RelationName?, string Direction, string Name, string Description }
```

- 每图独立解析 embedding 模型（镜像 wiki `ResolveModelAsync`：join ai_model+ai_channel 双 Enabled、公共或团队授权；KG 内实现并注明「与 WikiEmbeddingService 保持语义同步」）
- 一跳扩展复用 `IKnowledgeGraphStore.GetNeighborsAsync`（每命中实体邻居截断 ≤10）；`entityTypeName` 由 PG 类型表补齐
- `text` 文本化：每命中实体一段「节点名（类型）：描述 → 关系[方向] 邻居名（描述）」，截断总长（≤8KB）
- 团队门禁：对话路径由绑定保存时校验；工作流路径由节点 Client 守卫（非本团队 graphId 忽略，对齐 `WorkflowWikiSearchClient`）；团队检索 API 路径复用 `KnowledgeGraphAuthorizer`

## 6. 消费端

- **绑定**：`AppAgentConfigEntity.GraphIds`（JSON 数组文本，对齐 `WikiIds`）：保存 Handler 校验（非本团队托管图 400）、发布快照固化、`AppAgentFactory` 与 `WorkflowNodeAiInvoker` 解析进构建上下文
- **对话工具**：`GraphAppToolProvider : IAppToolProvider`（`src/ai/MoAI.AI.Core/Tools/`，紧邻 `WikiAppToolProvider`）：绑定非空时注册工具 `search_knowledge_graph`，描述说明「查询应用绑定的知识图谱中的实体关系，适合『A 和 B 什么关系/有哪些 X』类问题」；入参 `{query, topK?}`（topK 钳 1-20）；未配置 embedding 的绑定图计入 `SkippedHints` 回给模型
- **工作流节点**：`NodeTypes.KnowledgeGraphSearch = "kgSearch"`；Executor 配置 `{graphId, topK(默认5,上限50)}`、输入必填 `query`、输出 `{query,count,hits[],contents[],text}`；注册进 `WorkflowServiceCollectionExtensions`；团队守卫 Client 镜像 `WorkflowWikiSearchClient`

## 7. 前端

- 应用配置页（Agent）：「知识图谱」多选下拉（对齐 wiki 选择器交互与数据源 `getKnowledgeGraphs`）
- 图谱设置页：embedding 模型选择（模型列表 API 复用 wiki 同款）+ 维度展示
- 工作流编排：`NodeForm.tsx` 增 kgSearch 面板（graphId 下拉 + topK），`NodePanel` 分组、`constants` 图标/分类——三处均照 knowledgeSearch 现有写法
- 文案双语 i18n

## 8. 安全与边界

- 仅托管图参与；**接入图一期直接排除出应用绑定下拉**（绑定即检索，不能检索的不开放绑定；其查询诉求由 Text2Cypher 插件覆盖，绑定下拉数据源过滤 `mode === 'managed'`）
- 检索不返回 propsJson（内部属性 JSON 不外泄）；description 本就对外可见（管理页同权）
- 工具与节点均受应用/工作流既有审批与门禁约束，无新增授权面

## 9. 测试与验收

- **单测**：GraphSearchService（mock 集合与 store：topK/minScore/跳过提示/text 截断/邻居截断）、绑定校验 Handler、工具 Provider 注册条件（绑定空不注册）。挂 `tests/MoAI.KnowledgeGraph.Tests`（已引用 KG.Core + InternalsVisibleTo）
- **E2E**（新建 `local-dev/kg-search-e2e.mjs`，缩写 **KGS**，不复用既有缩写）：复用 wiki-recall-e2e 的本地 OpenAI 兼容桩渠道（embeddings），零 SKIP 闭环——KGS-S1 配置 embedding 模型、S2 建节点触发向量化（检索命中）、S3 改名后向量更新、S4 删节点后不再命中、S5 minScore 过滤、S6 未配模型图谱跳过提示、S7 团队检索 API 门禁（越团队 403/404）、S8 应用绑定保存校验、S9 工作流 kgSearch 节点（借 workflow 调试执行或经节点 Client 直调，按 WF e2e 惯例定）
- **文档四件套**：bdd 补 KGS 场景、tdd 补映射、sdd 更新消费端章节、AGENTS.md 验证命令登记

## 10. 新增/改动文件清单（供实现计划展开）

| 位置 | 动作 |
|---|---|
| `src/database/MoAI.Database.Shared/Entities/KnowledgeGraphEntity.cs` + Postgres 配置 + asserts 增量 SQL | 修改：EmbeddingModelId/EmbeddingDimensions |
| `src/knowledgegraph/MoAI.KnowledgeGraph.Core/Services/`（VectorStore/Record/EmbeddingService/GraphSearchService + MQ Consumer + 消息） | 新增 |
| `src/knowledgegraph/.../Handlers/`（节点 CRUD/批量导入/AI 导入/删图 Handler 挂触发；`QueryKnowledgeGraphSearchCommand`+Controller 端点） | 修改/新增 |
| `src/database/.../AppAgentConfigEntity.cs`（GraphIds）+ `SaveAppAgentConfigCommandHandler` 校验 + `AppAgentFactory`/`WorkflowNodeAiInvoker` 解析 + PublishedConfig 快照 | 修改 |
| `src/ai/MoAI.AI.Core/Tools/GraphAppToolProvider.cs` + 注册 | 新增 |
| `src/app/workflow/...`（NodeTypes/Executor/Client/注册） | 新增 |
| `ui/`（应用配置图谱多选、图谱设置 embedding、工作流节点面板、i18n） | 修改 |
| `tests/MoAI.KnowledgeGraph.Tests/` | 新增单测 |
| `local-dev/kg-search-e2e.mjs` | 新增 |

无数据库迁移之外的基建变更；pgvector 扩展与 MQ 基建均现成。

## 11. 二期展望

关系/边向量化与关系语义检索、rerank 接入（`WikiEntity.RerankModelId` 已有存储先例）、KG×wiki 混合召回与融合排序、召回测试 UI、答案挂 citation 子图（G6 画布复用）、自动注入模式（可配 RAG 式）。
