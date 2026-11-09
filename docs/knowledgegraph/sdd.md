# 知识图谱模块设计规格（SDD）

> 关联：[SDD](./sdd.md) ｜ [BDD](./bdd.md) ｜ [TDD](./tdd.md) ｜ [SOP](./sop.md) ｜ 上游：[../team/sdd.md](../team/sdd.md)、[../settings/sdd.md](../settings/sdd.md) ｜ 设计：[2026-09-10-knowledge-graph-design.md](../superpowers/specs/2026-09-10-knowledge-graph-design.md)、[2026-09-14-knowledge-graph-memgraph-canvas-design.md](../superpowers/specs/2026-09-14-knowledge-graph-memgraph-canvas-design.md)、[2026-09-22-kg-graph-search-design.md](../superpowers/specs/2026-09-22-kg-graph-search-design.md) ｜ 证据：[local-dev/kg-e2e.mjs](../../local-dev/kg-e2e.mjs)

- 日期：2026-09-14（v2）
- 状态：后端 v2 已实现（Memgraph 方言化 + 画布/邻接接口 + 权限收紧 + 名称全局唯一）；前端入口/画布已接线；E2E 待图数据库实例。v2.5（2026-09-21）：内置模板「运维服务」改为「物流运输」（实体类型支持预置属性定义）；图谱详情（托管/接入）默认进入图览。v2.6（2026-09-21）：新建图谱弹窗支持同步上传头像——前端「创建成功 → 存储直传 → 复用 `POST {id}/avatar` 登记」串联既有链路，零后端改动；登记失败不阻断创建，可在设置页补传。v2.7（2026-09-21）：物流运输模板一次性预置示例实例与关系（11 节点/15 边写入图库，失败清理残留并整体回滚），建图即得完整图览。v2.8（2026-09-21）：图览交互升级——节点可拖动、点击节点/边弹详情抽屉（节点属性含距离/运价）、边显示关系类型名。v2.9（2026-09-21）：**AI 导入文件生成图谱**——图览工具栏「AI 导入」（文档直传 chat 公共目录）→ 后端 Maomi.ToMarkdown 提取 → 对话模型按图谱现有模型抽取实体/关系（`KnowledgeGraphImportParser` 容错解析）→ 类型/约束校验后入图；详见 §5 `/{id}/import-file`。**SP-A 一期（2026-09-22）：图检索消费层已落地并 E2E 实跑通过**——实体向量化 + 检索 API + 应用绑定 + 对话工具 + 工作流节点五件，详见 §5.2 与 [设计文档](../superpowers/specs/2026-09-22-kg-graph-search-design.md)，场景 [@KGS-S1~S9](./bdd.md#feature-图检索消费层kgs-s)。 v3.1（2026-09-27）：**图览画布引擎切换 React Flow**——`@antv/g6` → `@xyflow/react` ^12 + d3-force 力导向；节点为 React 组件卡片、内置 Controls/全屏、连接桩拖线建边（替代两段式点击）、d3-force 同步布局 + 自算视口适配（经 onInit 实例）。v3（2026-09-27）：**图览瘦身 + 维护页改版**——详情菜单收敛为 图览/维护/设置（接入图 图览/模型/设置），图览头部步骤条与 AI 导入按钮移除、画布占满剩余高度/全部宽度并新增 缩放/适应/全屏 工具栏与加载上限选择（100/200/500，默认 200）；新增维护页步骤条（模型→实例→关系，`?step=` 路由），旧独立路径重定向兼容，AI 导入入口迁至「录入实例」步骤（v3.2 再移至独立「导入」菜单页：AI 智能导入 + JSON 结构化导入两页签，维护页不再保留导入入口）。
- 领域：`src/knowledgegraph/{Shared,Core,Api}`；单测 `tests/MoAI.KnowledgeGraph.Tests/`
- v1 设计：[前版 SDD 记录](../superpowers/specs/2026-09-10-knowledge-graph-design.md)（托管 + 接入双模式，D1~D13 仍有效）

## 1. 目标

向量检索无法回答多跳关系问题。知识图谱模块用图数据库存实体与关系，独立于向量知识库（两模块互不依赖，仅消费端可选同时绑定）：

- **托管图谱（`mode=managed`）**：团队可建多个图谱，手动维护 schema 与节点 / 边；建图可选内置模板；图谱详情默认「图览」画布浏览。
- **外部接入图谱（`mode=connected`）**：登记图数据库实例的某个 database，自动内省，默认只读。

能力由系统设置 `KG_ENABLED` 门禁；未开启时建图 / 接入与写操作 409。

## 2. 架构与模块

三层与 `wiki` 平级、互不依赖：`*.Shared` → `*.Core` → `*.Api`。Core 仅引用 `MoAI.Settings.Shared` 与 `MoAI.Team.Shared` 接口。

- `IKnowledgeGraphSettingsService`（settings 模块）读取 `KG_ENABLED / KG_URI / KG_USERNAME / KG_PASSWORD / KG_DIALECT`。
- **方言**：`KnowledgeGraphStoreSettings.Dialect`（`memgraph` 默认 / `neo4j`）。Memgraph 兼容 Bolt/openCypher，统一用 `Neo4j.Driver` 访问（D15）。方言只分叉探活 / 内省 / 索引三处（D14）：探活 neo4j 用 `db.labels()`、memgraph 用 `RETURN 1`；内省 neo4j 用 `db.*` 过程，memgraph 用**数据派生查询**（3.x 已移除 `mg.labels` 等过程，且 `labels(n)` 对单标签节点返回字符串而非列表，需归一化处理）；索引 memgraph 用 `CREATE INDEX ON :KgNode(id)`（重复创建按异常吞掉幂等），neo4j 用 `IF NOT EXISTS` 约束 + 复合索引。数据面 Cypher 不分叉。
- `GraphDriverProvider`（单例）按 `(uri, username, password, dialect)` 缓存 `IDriver`，变化时重建并异步释放旧实例；首次写入前幂等初始化索引。**无实时状态检测（v4.3）**：不做后台监听/定时探活/失败状态缓存——列表等读接口不触碰图数据库，图库不可用由具体操作端点连接失败自动报错（驱动建连超时 10s，快速 503 不挂起）；连通性预检只在系统设置开启前经 `/test-connection` 一次性实测。
- `CypherKnowledgeGraphStore`（scoped）封装全部 Cypher；**单库 + `kgId` 属性隔离多图谱**（Memgraph 多数据库为企业版 3.10+ 特性，社区版 `CREATE DATABASE` 被拒，故不做物理分库，见 D20）。节点属性名统一 `kgId`（v2 前代码曾用 `KnowledgeGraphId`，已统一）。
- connected 会话仅 neo4j 方言按库名路由（`WithDatabase`），memgraph 恒用默认库。
- `IKnowledgeGraphAuthorizer` 统一团队角色判定，并对 connected 图拦截写操作。

## 3. 数据模型

### 3.1 PostgreSQL（只存目录与 schema，不存图数据）

- `kg`：`id / team_id / name(50) / description(255) / mode(20, 默认 managed) / database(100, 仅 connected) / embedding_model_id(uuid, null=未配置不参与检索) / embedding_dimensions(int, 1-2000 默认 1024, SP-A) / is_deleted / 审计`
  - `template_key` 已退役：模板仅创建时作为种子类型数据参考（`CreateKnowledgeGraphCommand.TemplateKey`），不与图谱持久化关联，列表/详情不再回显；存量库经启动幂等 DDL `DROP COLUMN IF EXISTS template_key` 清除
  - 索引 `idx_knowledge_graph_team_id`；partial 唯一 `idx_knowledge_graph_name_live_uindex (name) WHERE is_deleted = 0`（v2 起名称**全平台唯一**，原 `(team_id, name)` 收紧）；向量两列存量库见 `asserts/knowledge_graph_embedding.sql`
- `kg_entity_type` / `kg_relation_type`：同 v1（关系类型带 `source_type_id / target_type_id` 起止约束）。

### 3.2 图库 keyspace

```cypher
(:KgNode { id: <guid>, kgId: <long>, entityTypeId: <long>, name, description })
-[:KG_REL { id: <guid>, kgId: <long>, relationTypeId: <long> }]->
(:KgNode { ... })
```

类型名 / 颜色回 PG 查询，改 schema 零迁移；`kgId` 隔离多图；删节点 `DETACH DELETE` 连带边；删托管图按 `kgId` 清空。模板目录（blank/logistics/org/event）代码内置只读，建图时一次性复制：**实体类型可携带预置属性定义**（物流运输模板：航段预置 运输方式/距离公里/运输价格/时效天），**且可携带示例实例与关系**（物流运输预置 4 港口/5 航段/2 承运商与 15 条 出发/抵达/承运 边，建图时经 `IKnowledgeGraphStore` 写入图库——按「港口-航段-港口」串联即可计算 A 到 C 的最短距离与运价；图库写入失败时 `PurgeGraphAsync` 清理残留并抛出，由建图事务整体回滚，D1~D13 见 v1 设计稿）。

## 4. 权限（v2 收紧：Member 全只读，Handler 判定）

| 操作 | Owner/Admin | Member | 非成员 |
|---|---|---|---|
| 建图 / 接入 / 改图 / 删图 | ✅ | 403 | 404 |
| 列表 / 详情 / schema / 内省 | ✅ | ✅ | 404 |
| 实体类型、关系类型增删改（仅托管） | ✅ | 403 | 404 |
| **节点 / 边增删改（仅托管，v1 曾放开 Member）** | ✅ | **403** | 404 |
| 画布 / 邻接（本期新增） | ✅ | ✅ | 404 |

前端按 `myRole` 隐藏写入口（实体 / 关系页）；列表 / 详情响应携带 `myRole` 与 `enabled`。

## 5. API（`/api/knowledge-graph`）

v1 全部端点保留（图谱 CRUD、模板、schema、类型 CRUD、节点 / 边 CRUD 与分页），v2 新增：

| 方法 | 路由 | 说明 |
|---|---|---|
| POST | `/test-connection` | **图数据库连通性预检（v4.3，系统设置开启前测试）** `{uri?, username?, password?, dialect?}`（可选覆盖，空=回退系统设置已保存的连接值）→ `{ok, latencyMs, dialect, error?}`；仅超级管理员（角色门禁在 Controller）。**临时 driver**（连接超时 5s，不复用 `GraphDriverProvider` 单例——不污染运行时连接）跑探活语句（与建接入图探活同口径：memgraph `RETURN 1` / neo4j `CALL db.labels()`），整体 8s `Task.WhenAny` 限幅（驱动不响应取消令牌，限幅后 Dispose 中断被放弃任务）；连接失败/超时返回 `ok=false` 携带原因**不抛 503**（设置页展示「测试未通过」）；非法地址/方言 400。设置页保存流程：开启=先保存连接项→实测→通过才落 `KG_ENABLED=true`（未通过不落开关、表单值保留可重试），关闭=仅写开关，场景 [@KG-S46](./bdd.md#kg-s46) |
| POST | `/{id}/canvas` | 有界子图 `{entityTypeId?, label?, relationTypeId?, keyword?, limit≤500 默认 200, depth(1~2 默认 1)}` → `{nodes(含 degree), edges, truncated}`，边仅返回节点集内部；托管图按 `entityTypeId` 过滤，接入图按 `label` 过滤（v2.1 起接入图开放）；depth=2 托管图以种子集再扩一跳（忽略过滤，受 limit 总预算约束，D38） |
| GET | `/{id}/nodes/{nodeId}/neighbors?limit=` | 一跳邻接（默认 100，上限 500）→ 同上结构（v4 起节点携 `degree`）；托管图按节点 id，接入图按 elementId（v2.1 起接入图开放） |
| POST | `/{id}/nodes/merge` | **实体合并（v4）** `{targetNodeId, sourceNodeIds(1~20), confirm}`：源端点替换目标重挂边（同三元组已存在跳过、两端均为源的自环丢弃）、描述拼接去重（≤2000）、propsJson 深合并（冲突目标优先）、单事务 DETACH 删源；起止类型约束违反 400、触边 ≥20 未 confirm 409；目标向量 upsert + 源向量删除增量；仅托管图 Admin+（D29） |
| GET | `/{id}/schema?refresh=` | 接入图 schema 走 Redis 内省缓存（TTL 5 分钟），`refresh=true` 强制重新内省并返回相对基线的 `changes`（v2.1） |
| POST | `/{id}/avatar` | 设置图谱头像 `{objectKey}`（v2.2）：仅 Owner/Admin；objectKey 须为已登记且完成上传的文件（伪造 404）；列表/详情回显 `avatarPath`（前端经 `/static/{key}` 解析） |
| GET | `/model-options?teamId=` | 团队可用 AI 模型选项（公开+已授权，仅团队成员）：v2.9 为 AI 导入提供对话模型，SP-A 起分桶返回 `conversationModels` / `embeddingModels` |
| POST | `/{id}/recall-test` | **召回测试（v3.4，对齐知识库召回测试）**：`{query, top, minScore?, aiModelId?, isOptimizeQuery?, isAnswer?}`——复用 GraphSearchService 向量召回（实体 + 一跳邻居）；AI 优化问题/生成回答（对话模型解析语义同 wiki：双 Enabled + 公共或团队授权 + conversation）；团队成员级（IUserIdContext + Controller SetUserContext） |
| POST | `/{id}/import-json` | **JSON 结构化导入（v3.2，导入页）**：`{content, mode?, autoCreateTypes?, validateOnly?}`——容错解析导入页提交的 JSON 文本（顶层 nodes/edges，解析错误带行定位），页面控件值覆盖 JSON 内同名字段；经 `KnowledgeGraphDataImportService` 与外部 /import 共用落库管线；仅托管图 Admin+；返回逐条导入结果 |
| POST | `/{id}/import-file` | **AI 导入文件生成图谱**（v2.9；2026-11-08 格式扩容 + **v4.1 异步任务化 D27**）：`{objectKey, fileName, aiModelId}` → **登记导入任务返回任务 id**（Hangfire 执行：提取文本（Maomi.ToMarkdown 原生 10 种直取；其余走 `MoAI.Storage.Shared/TextExtract/` 共享提取层——EPUB/ODT/RTF 自研提取器 + 代码配置类文本纯文本兜底，与知识库同口径，见 [../wiki/sdd.md](../wiki/sdd.md) D47；截断 1.2 万字符）→ 对话模型按图谱现有模型抽取实体/关系（JSON）→ **B1 幂等合并落库 D26**：节点按（类型+名称）命中合并（描述去重拼接/属性目标优先/`srcJson` 证据 FIFO 20）/未命中新建带证据、边按三元组已存在跳过 → 终态 done 携带统计（含 `nodesMerged`））；仅托管图 Owner/Admin；objectKey 须为公开 chat 目录已直传文件。上传白名单走专用端点 `POST /api/storage/public/pre_upload_graph_file`（`DocumentFormatConst.ExtractableFormats` 42 种、20MB；对话附件 `pre_upload_chat_file` 白名单不变） |
| GET | `/{id}/import-jobs` / `/{id}/import-jobs/{jobId}` / `POST .../retry` / `POST .../cancel` | **导入任务治理（v4.1）**：列表分页倒序 / 详情（状态 pending/extracting/parsing/merging/done/failed/cancelled + doneRows/totalRows + error + result 统计）/ 重试（仅 failed/cancelled，重置重入队，幂等合并不重复）/ 取消（pending 即时、执行中阶段间隙生效、终态 409）；仅托管图 Admin+，场景 [@KG-S42/S43](./bdd.md#kg-s42) |
| PUT | `/{id}/embedding-config` | 配置向量化模型 `{embeddingModelId, embeddingDimensions(1-2000)}`（SP-A，见 §5.2）：仅托管图 Owner/Admin；模型须启用、团队可用且 ModelKind=embedding（400）；接入图 409；配置变更清空旧向量集合并全量重嵌（≤5000 节点，超出告警） |
| PUT | `/{id}/extract-model` | **默认抽取模型（v4.2，B5/D30）**：`{extractModelId(uuid? 传 null 清除)}` → AI 导入与画布问图缺省对话模型；校验 启用+团队可用+conversation（400）；接入图 409；仅托管图 Owner/Admin；详情响应与前端类型同步携带 `extractModelId` |
| PUT | `/{id}/canvas-style` | **画布样式规则（v4.4，B8）**：`{captions: {实体类型id字符串→属性名|""}|null}`（≤50 条、键须 long、值≤100）→ 节点 label 追加「名 · 属性值」（值 `__type__`=类型名、""=不显示）；类型归属本图校验（400）、接入图 409、null/空清空；存 `knowledge_graph.canvas_style_json`（asserts 补列）；详情响应携 `canvasStyle`（解析容错）；仅托管图 Owner/Admin；场景 [@KG-S49](./bdd.md#kg-s49) |
| POST | `/{id}/search` | 图谱语义检索 `{query, topK, minScore?, neighborTopK(0-30 默认 10，v4.2 B3/D28)}` → `{hits[](邻居行携 degree), contents[], text, textTruncated, skippedHints[]}`（SP-A；v4.2 起邻居按度数降序取 Top、文本化按 8192 预算整段组装）：Member 可用；接入图 409、未配向量化模型 409 |
| POST | `/{id}/text2cypher` | **画布问图（v4.3，B7/D31）**：`{question(≤500), aiModelId?}` → schema 自描述 → 模型生成只读 Cypher（D30 回退：请求级 > 图谱默认 > 400）→ KG 本地只读守卫（`KgCypherReadOnlyGuard`，与 aiplugin 同规则集：剥注释/字符串/反引号后校验单语句+首关键字白名单+写关键字黑名单）→ `IKgCypherAccessService` 执行（`$kgId` 注入 + 结果侧归属校验，500 行/30s）→ `{ok, cypher, columns, rows, rowCount, truncated, nodeIds, error}`（nodeIds=结果行 Guid 命中本图节点，供画布高亮）；Member 可用、仅托管图（接入图走 kg_cypher_query 插件，409 提示）；场景 [@KG-S48](./bdd.md#kg-s48) |

均为只读、Member 可用。请求模型实现 `IModelValidator<T>`。

### 5.1 外部开放接口（`/api/teamres/knowledge-graph`）

- **授权模型（团队级）**：接入应用（access_app）创建即授权其团队的资源访问——应用 token（`UserType.ExternalApp`，携带 `TeamId` claim）对本团队托管图谱**等价团队 Admin**；`AccessAppEntity.AppIds` 白名单已删除。仅接受应用 token（用户/匿名/内部 JWT 一律 401/403）。
- **端点面（26 个）**：`POST list`（仅返回本团队 **managed** 图谱，connected 不对外暴露）、`GET {kgId}/schema`、实体类型/关系类型各 CRUD（3+3）、节点 list/详情/by-key/keys-list/一跳邻接/CRUD/批量/batch-delete（11，单节点 POST/PUT 支持可选 `key`）、边 list/详情/CRUD/批量/batch-delete（7）、**批量导入 `POST {kgId}/import`**（1）。
- **语义**：跨团队或不存在一律 404（不泄露存在性）；connected 图谱写操作 409；批量导入节点/边 ≤200 条/次，**先整批校验后写入，任一条失败整批 400 拒绝**（类型存在、边端点存在于该图谱、超上限校验层拒绝）；删节点 DETACH 级联。
- **批量导入（v3.0，`POST {kgId}/import`，面向 CSV/JSON 数据源解析后的结构化写入）**：节点 ≤500 + 边 ≤500/次；`mode` = `upsert`（默认，幂等重导）| `create`（一律新建）；`autoCreateTypes` 时实体/关系类型按名称自动创建（实体类型属性定义取该类型各行属性键并集、均 string，仅帮助画布渲染；关系类型约束为任意）。**节点匹配**：有 `key` 按 key 命中更新（图库 `KgNode` 增可选 `key` 属性，无 schema 迁移），key 未命中回退（类型+名称）匹配并**收养该 key**；无 key 按（类型+名称）匹配；未匹配新建。更新为**整体覆盖**（名称/描述/属性，属性未传视为清空；`key=null` 保留现有 key）。**边端点引用**：`nodeId` > `key` > `name`（name-only 命中多个时报错需补 `entityTypeName` 消歧；可引用本请求内刚导入的节点）。**失败语义与 nodes/batch 相反：行级失败逐条报告不阻断**（类型缺失/端点未命中/约束不符），仅结构性问题整单 400；upsert 模式下边按（起点,关系,终点）幂等跳过（created/updated/skipped 计数 + 逐行结果）。导入后一条向量增量消息带全部受影响节点。**疑似重复检测（v3.3）**：`detectDuplicates`（默认开）时对新建节点按「名称
描述」向量化（复用图谱向量化模型与 `__kg_{id}` 集合），对照已有节点（top3/行）+ 批内两两余弦，≥0.85 记入响应 `duplicateSuspects`（existing/inbatch 两类，供用户合并前核对）；未配置向量化/模型不可用/任何异常静默跳过，不阻断导入；单次最多检查 100 行。- **MCP 服务器（v3.1，`/api/teamres/knowledge-graph/{kgId}/mcp`）**：与知识库 MCP 同构——MCP streamable HTTP 无状态模式、ExternalAuthenticationMiddleware 统一鉴权（moai-ac- 接入 key/应用 token/本图 moai-tr- 资源 key + kgId 归属校验 404；原 `kg_mcp` 范围与读/写分档已随 2026-11-04 scope 体系退役——moai-ac- 全量放行，其它图的 moai-tr- key 403 insufficient_scope）；四只读工具 `list_knowledge_graphs`/`get_knowledge_graph_schema`/`search_knowledge_graph_nodes`/`search_knowledge_graph_recall`（召回复用 §5.2 GraphSearchService：未配向量化返回 skippedHint 可读说明不报错、接入图 409）。**工具域隔离**：宿主与知识库 MCP 共享一个 McpServerOptions，`KnowledgeGraphMcpToolGate`（KG.Core/Mcp）以 AddListToolsFilter/AddCallToolFilter 按请求路径分域——各端点 tools/list 只列本域工具、跨域已知工具调用拒绝、未知工具放行 SDK 原生 -32602。
- **同步闭环三件**（v3.0 后补）：`POST {kgId}/nodes/batch-delete`（按 key 批删 ≤500，返回实际删除数）、`GET {kgId}/nodes/by-key/{key}`（同步核对，404 不存在）、`POST {kgId}/nodes/keys/list`（分页枚举已落 key 节点 key/nodeId/name/entityTypeId，全量比对找删除差集）；`POST {kgId}/edges/batch-delete`（≤500 行，关系类型按 id/名称、端点按 nodeId/key/名称引用删边——同三元组平行边全删，端点/关系类型未命中按行失败，无边幂等计 0；**建议同步顺序：import upsert → edges/batch-delete → nodes/batch-delete**，节点删除会 DETACH 级联边）；`import` 增 `validateOnly`（只读预演：完整走类型解析/匹配/端点解析/约束校验并返回预测计数与逐条结果，不建类型、不写图库、不发向量增量，created 行 id 恒空——CSV 管道可先预检后写入）。
- **Caller 注入模式**：Controller `RequireCaller()` 从 `HttpContext.Items` 取 `ExternalTokenContext` 组装 `ExternalGraphCaller{TeamId, AccessAppId, OnlyGraphId}`（OnlyGraphId 供团队级 list 过滤——图资源 key 只见绑定图）随命令下发；Core 侧 `IExternalKnowledgeGraphAuthorizer.AuthorizeAsync`（原 `write` 读写分档参数已随 2026-11-04 scope 退役删除）统一 404/409，Handler 复用 `IKnowledgeGraphStore` 与内部命令同款校验规则（角色门禁替换为外部授权器），场景见 bdd `@KX-01…KX-12`、证据 [kg-external-e2e.mjs](../../local-dev/kg-external-e2e.mjs)。

### 5.2 图检索消费层（SP-A 一期，五件）

> 设计与二期边界见 [2026-09-22-kg-graph-search-design.md](../superpowers/specs/2026-09-22-kg-graph-search-design.md)；仅托管图参与，接入图继续由 Text2Cypher 插件覆盖。

- **向量化**：节点 name+description 以「名称\n描述」嵌入 `__kg_{id}` pgvector 集合（HNSW+Cosine，维度取图谱配置）；10 处触发点（建图模板种子、节点增/改/删、外部节点增/改/删/批量、AI 导入、embedding-config 全量）经 MQ `kg.node.embedding` 发 delta 消息，Consumer Qos=1 **幂等消费**（upsert 读图库最新状态、同名多次 delta 自然合并；全部失败才抛出走重投，重试耗尽 Ack 放弃）；未配模型静默跳过；`IKnowledgeGraphStore` 删图链路在软删登记**前**清空向量集合（软删后 IsDeleted 全局过滤器会使维度解析失败，清理失效）。
- **检索服务**：`GraphSearchService` 每图独立解析 embedding 模型（镜像 wiki `ResolveModelAsync`：双 Enabled + 公共或团队授权，KG 内实现并注明语义同步）→ 查询向量召回 top 实体 → 复用一跳邻接（邻居 ≤10）补方向/关系名/类型名 → `GraphSearchTextHelper` 片段文本化（**与工作流 kgSearch 节点共享**，两侧同源同形）。
- **应用绑定**：`AppAgentConfigEntity.GraphIds`（JSON 数组，对齐 `WikiIds`）——保存校验仅本团队托管图（越权/接入图 400），发布快照固化，`AppAgentFactory` 与 `WorkflowNodeAiInvoker` 双装配点解析进构建上下文；登记见 [../app/sdd.md](../app/sdd.md)。
- **对话工具**：`GraphAppToolProvider`（`src/ai`，Order=21）在绑定非空时暴露 `search_knowledge_graph`（Kind=graph，topK 1-20 每图、payload 16KB 预算 + `hitsTruncated`）；登记见 [../ai/sdd.md](../ai/sdd.md)。
- **工作流节点**：`kgSearch`（config.graphId 静态 + topK 1-50 默认 5，输入必填 query，输出 `{query,count,hits,contents,text}`，text 8192 截断），Guard 于 draft/publish/debug-run 三入口校验 graphId 团队归属（400）。
- **前端**：图谱设置页向量化模型配置（Admin+）、应用配置「知识图谱」多选（managed 过滤）、工作流节点面板与 i18n。

## 6. 关键流程与校验（Core）

- 能力门禁、建图模板复制事务、接入探活、connected 只读、类型删除引用拦截、节点 / 边起止约束校验均同 v1（见 v1 设计稿 §6）。
- **可空 Guid 判空约定（SP-A T1 审查确立）**：`EmbeddingModelId == null` 判空，**禁止 `== Guid.Empty` 哨兵**——KG 内统一（`KgEmbeddingService` / `GraphSearchService` / 检索与配置 Handler 均带注释），后续新增可空 Guid 字段照此办理。
- **名称唯一（v2）**：预检 `AnyAsync(x => x.Name == …)` 不再限定团队；数据库约束 `idx_knowledge_graph_name_live_uindex` 兜底 409。
- **图库不可达**：`ServiceUnavailableException` 等统一映射 503「无法连接图数据库」（v1 落 500 的遗留已修复）。**列表不承担探测职责（v4.3，@KG-S34 退役改写）**：`GET list` 只读业务库（设置开关 + 图谱行），响应项不携带实体/关系计数（原「逐图实时计数 + 3 秒限幅 + 超时任务旁路泄漏」方案整体移除——图库宕机时每次列表请求泄漏 N 个驱动重试任务）；数量在进入详情/模型页时按需统计。
- 画布查询：先取节点子集（`ORDER BY name LIMIT limit+1` 判截断），再取 `s.id IN $ids AND t.id IN $ids` 的内部边（上限 `min(limit*5, 5000)`）。
- **模型属性设置（v2.3）**：实体类型新增属性定义（`knowledge_graph_entity_type.properties` jsonb，数组 `{name,type(string/number/boolean/date),required,description}`，≤50 项、名称唯一 ≤100）；实例属性值以单个 `propsJson` 字符串属性存储在图库 `KgNode`（`SET n.propsJson`），查询时反序列化到节点项 `properties` 字典；实例表单按所选类型的属性定义渲染（number→InputNumber、boolean→Switch、date→DatePicker），必填属性前端校验。
- **接入图动态识别（v2.1）**：外部节点无统一 schema，以 `elementId(n)` 定位、`coalesce(toString(n.name), toString(n.title), toString(n.id), elementId(n))` 启发式取名、首个标签作实体类型（`labels(n)` 经 Memgraph 单标签字符串归一化）；label 过滤值经反引号校验后注入 Cypher。内省结果经 `KnowledgeGraphIntrospectionCache` 缓存：`kg:introspect:{kgId}:{db}`（TTL 5 分钟）+ `kg:introspect:baseline:{kgId}:{db}` 长期基线，新鲜内省时与基线 diff 出新增/消失的标签与关系类型（`changes` 字段）；删图时清理两 key。

## 7. 前端（v2 接线）

- 路由：`/knowledge-graph`（跨团队卡片墙，对齐 wiki 模式）+ `/team/:teamId/kg/:graphId/:section?`（详情）；团队详情独立「知识图谱」分区（`TeamKnowledgeGraphs`，工具栏 `PageToolbar`：关键字搜索框客户端过滤名称/描述（回车或「查询」触发，@KG-S31）+ 共 N 条 + 新建知识图谱，建图 / 接入 / 编辑 / 删除），Member 可见但只读。
- 列表卡片（2026-11-07 建，2026-11-08 实时计数退役改写 @KG-S32）：与知识库列表卡片同构——头像+名称+元信息行（接入图徽标「外部接入 · 只读」）→ 两行截断描述 → 分隔线页脚（创建时间+编辑/删除），列宽同知识库（xs24/sm12/md8/lg6/xxl4）。**实时资源数量展示已退役**：列表接口不携带 `entityCount/relationCount`（列表不触碰图数据库），数量在详情/模型页按需统计；设置页「知识图谱」卡片新增「测试连接」按钮（表单值直测），开关闭合保存=先连接项→实测→通过才落开关（@KG-S46）。
- 侧边栏 mainNav 新增「知识图谱」（`ClusterOutlined`）；`/team/*/kg/**` 高亮该入口。
- 详情 section（v3 改版，2026-09-27）：托管图菜单收敛为 图览 / 维护 / 设置（`KnowledgeGraphDetail`）；维护页（`KnowledgeGraphMaintenance`，路由 `/maintenance?step=`）头部步骤条承载 ① 定义模型 → ② 录入实例 → ③ 连接关系，点步骤切换；旧独立路径 `/schema`、`/entities`、`/relations` 渲染期声明式 `<Navigate>` 重定向到维护页对应步骤（接入图 `schema` 仍是合法菜单，接入图仅 图览 / 模型 / 设置 + 只读徽标）。托管图与接入图进入详情均默认「图览」（`@antv/g6` v5 力导向，类型色板过滤、关键字搜索、点节点一跳展开、空态引导）。页内互跳（查看实例/查看关系/去定义模型/关联）统一指向 `/maintenance?step=…` 并用函数式 `setSearchParams` 保留过滤参数。
- 图览交互（v2.8）：节点可拖动调整位置（编辑/只读态一致）；**建关系改两段式点击**（编辑态点起点节点再点终点节点，弹关系选择窗，替代 v2 的拖拽连线——与节点拖动手势冲突）；边中段显示关系类型名标签；点击节点弹详情抽屉（无遮罩，类型/描述/属性按模型定义排序，`mask=false` 不阻挡后续画布点击），点击边弹关系详情（关系名/起止）。注意 G6 v5 点击命中判定用 `event.targetType`（`target.type` 恒 undefined，v2~v2.7 点击展开实际失效）；Kiota 将节点 `properties` 字典收进 `additionalData`，消费方统一经 `flattenNodeProperties` 归一化。
- 图览页（v3）：头部不再渲染四步 Steps 与 AI 导入按钮（导入入口经维护页「录入实例」步骤（v3.0~v3.1）最终收敛至独立「导入」菜单页）；画布容器高度自适应（`innerHeight - 画布顶 - 24`，小屏保底 320px，`ResizeObserver` + `fullscreenchange` 重算），宽度占满内容区。大图保护：加载上限选择 100/200/500（默认 200，后端钳制 ≤500），截断时 Alert 提示含当前上限。
- 图览画布引擎（v3.1，2026-09-27）：由 `@antv/g6` v5 切换为 **React Flow（`@xyflow/react` ^12）+ d3-force 力导向布局**（G6 已卸载，项目内无其他引用）。节点为自定义 React 组件（类型色点 + 名称 + 类型名），边带箭头与关系名标签，点状背景；`Controls`（缩放/适应）+ `Panel` 原生全屏按钮（`requestFullscreen`，全屏态图标切换）；抓手拖画布/滚轮缩放/拖节点均为默认行为。点节点弹详情抽屉并展开一跳邻接（不重置视口），点边弹关系详情；右键画布新建实体、右键节点/边删除；**建关系由两段式点击改为连接桩拖线**（节点上下 `Handle`，`ConnectionMode.Loose`，仅可编辑托管图显示）。布局用 d3-force 同步模拟 280 ticks（charge -420 / link distance 150 / collide 56），老节点沿用上次坐标作初值保证展开时视觉连续，拖动位置回写布局缓存。视口适配自实现：按布局 bounds + 节点估计外包尺寸直接 `setViewport`（`fitPendingRef` 置位，初次加载/重新加载/筛选/搜索/上限变化后消费，展开不触发）；**必须经 `onInit` 下发的 instance 调用**——`useReactFlow` 的 helper 在挂载时序下 panZoom 未就绪会静默失败（实测 setViewport 返回 false），`fitView` prop 的自动适配与节点异步测量竞争会产生错误缩放，均不可用。
- 图览画布 v4（2026-11-08，D25 切回 G6，设计稿 [2026-11-08-kg-redesign-design.md](../superpowers/specs/2026-11-08-kg-redesign-design.md)）：`KnowledgeGraphCanvas.tsx` 重写为编排器 + `canvas/` 模块群（GraphCanvas 命令式 G6 封装 / CanvasToolbar / LegendPanel / DetailSidebar / MergeNodesModal / types 布局算法）。**引擎 `@xyflow/react` → `@antv/g6` 5.1**（动态 import 路由级分包；jsdom 无 canvas 2d，测试须 `vi.mock('@antv/g6')`——G6 渲染管线初始化炸异步 promise 不可 try/catch）；**布局仍自算烘焙进 datum.style**（force/circular/grid 三选，d3-force 沿用 v3.1 参数与位置连续逻辑）规避 G6 布局配置差异。交互新增：双击展开/收起（expandedBy 记账，收起回收仅本次引入且无其它连边且未选中/高亮/自展开的节点）、左下图例类型计数**多选显隐**（内存过滤替代顶部单选 Tag 服务端重载）、搜索定位（画布内高亮居中，未命中弹窗转服务端关键字重载）、详情侧栏关联关系列表可点跳转邻居 + 度数（节点大小按 √degree 映射）、Ctrl+多选 → 连关系（选中序=起终点，替代 Handle 拖线）/合并对话框（差异摘要 + Popconfirm 不可逆二次确认，`confirm:true` 直传后端）、右键菜单经 antd Dropdown 定位、边标签显隐设置；召回测试经 `?highlight=` 联动画布（缺失节点邻接补拉 ≤10 后高亮聚焦），`?merge=` 预选开合并。xyflow 与 d3-force 前者已卸载（全仓仅 KG 在用），后者保留为布局算法依赖。
- Member 只读：实体 / 关系页按 `myRole` 不渲染新增 / 编辑 / 删除入口。
- Member 只读：实体 / 关系页按 `myRole` 不渲染新增 / 编辑 / 删除入口。

## 8. 关键决策（v2，D14~D21；v4 增 D25/D29/D38）

完整论证见 [v2 设计稿](../superpowers/specs/2026-09-14-knowledge-graph-memgraph-canvas-design.md#决策)：**D14** 单实现内按 dialect 分支方言差异；**D15** 统一 `Neo4j.Driver` 访问两方言；**D16** 入口对齐 wiki 平级模式；**D17** Member 全只读（v1 放宽条款废除）；**D18** 画布本期只读；**D19** 画布仅托管图；**D20** 单库 + `kgId` 逻辑隔离（Memgraph 多数据库系企业版特性）；**D21** 图谱名称全局唯一。v2.1 新增：**D22** 接入图开放画布/邻接（elementId 定位 + 启发式取名，覆盖“外部服务直写图库、平台只读转接”场景）；**D23** PG 保留目录/权限/约束建模（不做 source of truth 迁移），仅动态内省结果落 Redis 缓存 + 基线 diff；**D24** 属性定义存实体类型 jsonb 列、实例属性值以 `propsJson` 单属性存图库节点（Memgraph/Neo4j 属性值不支持嵌套 map，单 JSON 字符串属性避免键污染且便于整体覆盖）；v2.3 顺带修复 v1 遗留：Update 节点/边/实体类型/关系类型 4 个命令 validator 校验路由字段导致所有编辑操作 400。v4 新增（2026-11-08，重构设计稿 [2026-11-08-kg-redesign-design.md](../superpowers/specs/2026-11-08-kg-redesign-design.md)）：**D25** 画布引擎切回 G6 5.1、卸载 xyflow（当年切走是编辑器型需求 + targetType 命中坑，本次为探索型需求反转；xyflow 全仓仅 KG 在用无共存成本；布局自算规避 G6 布局 API 面）；**D29** 实体合并语义（target 保留 + 边重挂同三元组去重 + 自环丢弃 + 描述拼接/属性深合并目标优先 + 向量重发；不可逆，触边 ≥20 二段确认）；**D38** canvas 子图深度参数（1~2，扩跳忽略过滤受 limit 预算约束）。v4.1 新增（P0a 落地）：**D26** 导入证据链——`KgNode.srcJson` 单属性存 {批次,文档名} 数组（沿用 D24 模式），AI 导入命中（类型+名称）即幂等合并（描述去重拼接/属性目标优先/证据 FIFO 20）、边按三元组跳过（仅 AI 导入路径；外部 /import 与 JSON 导入维持文档化覆盖语义不变）；**D27** AI 导入异步化——import-file 契约变更为返回 jobId（`kg_import_job` 状态机 + Hangfire 一次性任务 [AutomaticRetry(0)] 自管重试，不引入 Hangfire 自动重试的双跑风险）。v4.2 新增（B3/B5）：**D28** 检索邻居度数优先——邻接扩展按 size((m)--()) 降序取 neighborTopK（默认 10）、同邻居多关系保持每边一行、文本化按 8192 预算整段组装并置 TextTruncated（画布展开仍名称序不受影响）；**D30** 抽取模型三级回退——kg.extract_model_id 图谱级默认（PUT {id}/extract-model 可空清除），import-file 的 aiModelId 可空按 请求级>图谱默认>400 回退。v4.3~v4.4 新增（B7/B8）：**D31** 画布问图走独立只读端点（text2cypher，Member 可用；KgCypherReadOnlyGuard 本地实现与 aiplugin 同规则集；nodeIds 供画布高亮）；**D32** 画布样式规则——canvas_style_json 单列存 {captions}（键=类型 id、值=属性名/__type__/" "），Admin 配置全图生效，label 单行「名 · 值」拼接规避 G6 多行 label 不确定性。

## 9. 已知问题 / 下阶段

- 存量开发库若存在旧唯一索引 `(team_id, name)`，需手动 `DROP INDEX` + 按 EF 配置重建（`EnsureCreated` 不做增量演进）；旧设置键 `OPEN_NEO4J/NEO4J_*` 不迁移，超管需在设置页重配一次。
- 托管图名称/类型等混合「路由 + 请求体」绑定的命令，自动模型校验发生在路由回填前，validator 不校验路由字段（与 wiki 等模块约定一致，v1 曾误加规则导致 400，v2 已修复）。
- 接入图画布/邻接依赖 `elementId()`，要求 Memgraph ≥ 2.14（Neo4j 4.x+ 均支持）；外部直写数据的质量与租户隔离由接入方负责。
- **Memgraph 方言差异（2026-11-09 实测）**：`size((n)--())`（pattern 作 size 参数，openCypher 标准）Memgraph 3.13 不支持（报 `'size' argument must be either 'numeric...'`），度数计算统一用 `size([(n)--() | 1])` 模式推导（Neo4j 双兼容，store 内 4 处）；空结果集下 Memgraph 对该表达式惰性求值不报错，直连验证须用有数据的图（假阳性教训）。图库 503 文案已携带底层异常类型与消息便于排障。
- 外部接口 schema 计数为逐类型计数（N+1）：类型很多时图库往返次数多，量级可控，暂不合并为单条 Cypher。
- 外部接口「删除类型」与「批量写」存在毫秒级并发竞态（删除检查与批量校验非同一事务）；面向可信调用方可接受。
- 平台存在双错误格式：`ExternalAuthenticationMiddleware` 的 `{error:...}` 与全局管道的 `BusinessValidationResult` 并存（既有行为，外部调用方需两者都兼容）。
- 消费端已落地：**Text2Cypher 查图插件**（2026-09-21）——内置动态插件模板 `kg_cypher_query` + KG 访问服务 `IKgCypherAccessService`（托管图 `$kgId` 门禁与结果侧 kgId 归属校验、接入图按库路由、schema 自描述）+ teamplugin 实例保存归属校验（越团队 403/图谱不存在 404），Agent 对话模型即席只读查图；设计见 [2026-09-21-kg-text2cypher-plugin-design.md](../superpowers/specs/2026-09-21-kg-text2cypher-plugin-design.md)，场景 [@KT-S1~S10](./bdd.md#feature-text2cypher-查图插件消费kt-s)。**图检索消费层 SP-A 一期**（2026-09-22）已落地（见 §5.2）。
- SP-A 二期展望：关系/边向量化与关系语义检索、rerank 接入、KG×wiki 混合召回与融合排序、召回测试 UI、答案挂 citation 子图、全量重建进度可见、自动注入模式（见 [设计文档 §11](../superpowers/specs/2026-09-22-kg-graph-search-design.md)）。
- 后续迭代：AI 抽取入图（审核流）、画布编辑、结构化导入、Cypher 控制台。

## 资源命名规则（2026-11-07）

- 知识图谱名称统一约束：正则 `^[\u4e00-\u9fa5a-zA-Z0-9_]+$`（仅中文、英文、数字、下划线），`Create/UpdateKnowledgeGraphCommand.Validate` 落 `Matches` 规则；**仅约束图谱资源名**——节点/实体类型/关系类型名称为图内容数据不设此限；KG 模板「空白 / 自定义」仅为模板下拉显示名不落资源名；前端 `TeamKnowledgeGraphs`/`KnowledgeGraphSettings` 名称表单同步 pattern 校验。行为见 [@KG-S1](./bdd.md#kg-s1)。
