# 知识图谱 v2 设计（Memgraph 图库 + 平级入口 + 只读画布 + Member 只读收紧）

- 日期：2026-09-14
- 状态：待评审
- 前版：[2026-09-10-knowledge-graph-design.md](./2026-09-10-knowledge-graph-design.md)（v1：托管图谱 + 外部接入，后端已实现）
- 关联：[知识图谱 SDD](../../knowledgegraph/sdd.md) ｜ [知识库 SDD](../../wiki/sdd.md) ｜ [CQRS 规范](../../cqrs-conventions.md) ｜ [前端规范](../../../ui/docs/frontend-conventions.md) ｜ [部署](../../deployment/sdd.md)

## 背景与目标

v1 后端（托管图谱 CRUD + 外部 Neo4j 接入）与前端六个页面均已落地，但前端路由与导航从未接线，产品没有可用入口；图库选型由 Neo4j 切换为 Memgraph；同时用户旅程缺「看图」——现有实体/关系页是两张表格，没有「图」的体验。

v2 交付四件事：

1. **图库切换 Memgraph**：`IKnowledgeGraphStore` 契约不变，实现与配置方言化；connected 模式保留接入外部 Neo4j 的能力。
2. **入口接线（对齐 wiki 平级模式）**：一级导航「知识图谱」+ `/knowledge-graph` 跨团队卡片列表 + `/team/:teamId/kg/:graphId/:section?` 详情；资源归属团队、入口平级，与 wiki 完全对称。
3. **只读图览画布**：新 section「图览」，按类型过滤、搜索定位、点选高亮、一跳展开；本期只读不编辑。
4. **权限收紧**：节点/边写操作从「Member 可写」改为 Admin+，Member 全只读（v1 权限表中的放宽条款废除）。

AI 抽取入图、应用绑定 `graph_ids`（GraphRAG）、connected 图画布、画布编辑均为后续迭代，见文末。

## 范围

**包含：**
- 图库层：`Neo4jKnowledgeGraphStore` → 方言化 Cypher 实现（数据面 Cypher 不变）；设置模型/键中性化并新增 `dialect`；docker-compose `neo4j` → `memgraph`（含持久化参数）；图库不可达 500 → 503（v1 遗留）
- 图谱名称唯一性收紧：`(team_id, name)` → 全局 `name` 唯一（产品决策，跨团队不得重名）
- 权限：托管图节点/边增删改 Handler 收紧为 Admin+；前端按 `myRole` 隐藏写入口
- 前端入口：路由两条、AppSider mainNav 一项、`kg`/`graphId` 参数不一致修复、i18n zh-CN/en-US
- 图览画布：新增画布查询 API（有界子图 + 一跳邻接）+ `@antv/g6` 画布 section（仅托管图）

**不包含（后续迭代）：**
- 文档/文本 LLM 抽取入图；应用绑定知识图谱与图检索问答
- connected 图画布渲染、画布上增删改、结构化导入、Cypher 控制台、公开图谱

## 图库层（Memgraph 替换）

**驱动与连接**：Memgraph 兼容 Bolt/openCypher，保留 `Neo4j.Driver` 包直连。`Neo4jDriverProvider` 改名 `GraphDriverProvider`，缓存键仍为 `(uri, username, password)`（dialect 只影响查询，不影响连接）。

**方言分支（`CypherKnowledgeGraphStore`，原 `Neo4jKnowledgeGraphStore` 改名）**：

| 操作 | neo4j | memgraph |
|---|---|---|
| 探活 | `CALL db.labels()` | `CALL mg.labels()` |
| 内省 | `db.labels()` / `db.relationshipTypes()` / `db.propertyKeys()` | `mg.labels()` / `mg.relation_types()` / `mg.property_keys()` |
| 约束/索引 | `CREATE CONSTRAINT … IF NOT EXISTS` / `CREATE INDEX … IF NOT EXISTS` | `CREATE INDEX ON :KgNode(…)`（重复创建按异常吞掉实现幂等，落地时验证） |

节点/边的 MATCH/CREATE/DELETE/DETACH DELETE 数据面 Cypher 两方言一致，不分叉。托管 keyspace 固定 `:KgNode` + `KG_REL` + `kgId` 属性不变；顺手统一代码中 `KnowledgeGraphId` 属性名为 `kgId`（对齐 v1 设计稿，托管 keyspace 内改名零风险）。

**隔离模型（评审确认）**：Memgraph 多数据库（multi-tenancy）为企业版 3.10+ 特性，社区版无 license 时 `CREATE DATABASE` 直接被拒。v2 维持**单库 + `kgId` 属性逻辑隔离**（v1 已实现并在跑），托管图谱继续零库名；每图一库（`kg_{id}` 物理分库）依赖企业版 license 或外部 Neo4j，列为后续演进，`IKnowledgeGraphStore` 接口不因分库变化。

**设置**：`Neo4jKnowledgeGraphSettings` → `KnowledgeGraphStoreSettings`，新增 `Dialect`（`memgraph` 默认 / `neo4j`）。设置键 `OPEN_NEO4J / NEO4J_URI / NEO4J_USERNAME / NEO4J_PASSWORD` 更名为中性键并补 `KG_DIALECT`（键名落地时对齐 `SettingKeys` 现有风格；功能未 GA，不做旧键迁移，超管重配一次）。超管设置页文案改「图数据库」，加方言下拉。connected 模式在 `dialect=neo4j` 时仍可接外部 Neo4j database；`dialect=memgraph` 时 `database` 固定默认库（Memgraph 无多 database 概念）。

**部署**：docker-compose `neo4j` 服务替换为 `memgraph/memgraph-platform`（7687 Bolt / 3000 MgLab 调试 / 7444），`MEMGRAPH` 参数携带 `--storage-snapshot-interval-sec=300 --storage-snapshot-on-exit=true`，卷挂 `/var/lib/memgraph`；**社区版默认纯内存，持久化参数是硬要求**（漏配则容器重启丢图）。`neo4j_data` 卷更名 `memgraph_data`；sop 补数据持久化说明。具体镜像 tag 与参数在实现时以官方文档核验。

## 权限（v1 表的一处收紧）

| 操作 | Owner/Admin | Member | 非成员 |
|---|---|---|---|
| 建图 / 接入 / 改图 / 删图 / schema 增删改 | ✅ | 403 | 404 |
| 列表 / 详情 / schema / 内省 | ✅ | ✅ | 404 |
| **节点 / 边增删改（v1 为 ✅，本期收紧）** | ✅ | **403** | 404 |
| 节点 / 边查询、画布、邻接（本期新增） | ✅ | ✅ | 404 |
| 节点 / 边写（接入图） | 409 只读 | 409 | 404 |

后端改节点/边写 Handler 的 `adminOnly: false → true`；前端实体/关系页按 `myRole` 隐藏新增/编辑/删除入口。bdd 对应场景断言改为 403。

## 入口与路由（前端）

- 路由（`ui/src/router/index.tsx`）：
  - `{ path: 'knowledge-graph', element: <KnowledgeGraphList /> }`
  - `{ path: 'team/:teamId/kg/:graphId/:section?', element: <KnowledgeGraphDetail /> }`
- `KnowledgeGraphList` 跳转统一为 `/team/${teamId}/kg/${graphId}/…`（修复与 Detail `graphId` 参数名不一致）；`KnowledgeGraphDetail` 参数声明同步为 `graphId`
- `AppSider` mainNav 在「知识库」后追加 `{ key: 'knowledgegraph', icon: ClusterOutlined, labelKey: 'nav.knowledgeGraph', path: '/knowledge-graph' }`；`pathToKey` 补 `/knowledge-graph`，`/team/:teamId/kg/**` 高亮同 key
- i18n：`nav.knowledgeGraph` 等键 zh-CN/en-US 同步（页面既有 key 核对补齐）；列表页沿用既有 `enabled=false` 未开启引导

## 图览画布（新增，只读）

**API 增量**（`KnowledgeGraphController`，均为读、Member 可用，仅托管图，接入图 409）：

| 方法 | 路由 | 说明 |
|---|---|---|
| POST | `/{id}/canvas` | 有界子图：`{entityTypeId?, relationTypeId?, keyword?, limit≤500 默认 200}` → `{nodes, edges}`（边仅返回节点集内部的边） |
| GET | `/{id}/nodes/{nodeId}/neighbors` | 一跳邻接 → `{nodes, edges}` |

`IKnowledgeGraphStore` 新增 `QueryCanvasAsync` / `GetNeighborsAsync`（Cypher 先取节点子集再取内部边，两查询拼装，总量受 limit 硬约束）。

**前端**：`KnowledgeGraphDetail` 新增第一个 section「图览」（`@antv/g6` v5，新依赖，与 antd 同属 AntV 生态）。交互：

- 初始加载拉取有界子图；达到 limit 提示「已截断，请用过滤缩小范围」
- 左侧图例 = 实体类型色板（取自 schema），点选按类型过滤；搜索框定位节点并居中
- 点节点：高亮一度邻接 + 「展开邻接」按钮加载一跳（节点去重合入画布）
- 空态：引导去「实体」页录入或待后续 AI 抽取
- 本期纯浏览；新增/编辑仍在「实体」「关系」表格页完成

## 失败处理

- 图库不可达：画布/邻接/写操作返回 503（修复 v1 落入全局 500 的遗留问题），前端画布区显示重试
- 设置未开启/未配置：建图与图操作 409（沿用 v1 门禁与文案）
- 画布请求参数越界（limit > 500）：400

## 验证

- `dotnet build src/MoAI/MoAI.csproj` 0 error；`tests/MoAI.KnowledgeGraph.Tests/` 补画布/邻接/方言分支单测
- `local-dev/kg-e2e.mjs` 扩展：canvas / neighbors 场景、Member 写节点边 403 场景
- `cd ui && npm run typecheck && npm run lint && npm run test`；新增 List 路由、权限只读渲染、画布组件（mock G6）测试
- 手动走查：`docker compose up memgraph` → 超管开启并配置（dialect=memgraph）→ 套模板建图 → 录数据 → 图览过滤/展开/搜索 → 重启容器验证图数据仍在
- 提交前同步四件套：bdd 增改 `@KG-S<n>` 场景 → tdd 补映射 → sdd 状态/决策/权限表 → sop 部署与排障（Memgraph 持久化、MgLab、内省差异）

## 决策

- **D14** 图库抽象保留 `IKnowledgeGraphStore`，以 `dialect` 分支方言差异而非按图库分实现类：数据面 Cypher 占绝对多数，分叉点只有探活/内省/DDL 三处。
- **D15** 保留 `Neo4j.Driver` 作为两方言的统一驱动（Memgraph Bolt 兼容），不引入 memgraph 客户端包。
- **D16** 入口对齐 wiki 平级模式（一级导航 + 团队命名空间详情），不做「知识」聚合导航与团队详情 tab：与既有信息架构对称、改动最小；聚合导航留作知识类功能增多后的演进。
- **D17** Member 收紧为全只读：图谱定位为受控知识资产，schema 与数据写入统一 Admin+；与 v1 「图内容放开成员」相比是产品定位变化，非实现回退。
- **D18** 画布本期只读：先交付「看图」体验，画布编辑与表格编辑双入口的交互成本高，待使用反馈后定。
- **D19** 画布仅托管图：connected 图是外部 schema 的投影，渲染需要另一套标签/属性映射，独立成后续迭代。
- **D20** 隔离维持单库 + `kgId` 逻辑隔离：Memgraph 多数据库是企业版 3.10+ 特性（无 license 时 `CREATE DATABASE` 被拒，官方文档已核实），社区版兼容优先；`kg_{id}` 物理分库待企业版/Neo4j 场景再演进。
- **D21** 图谱名称全局唯一（跨团队不得重名）：产品定位为受控知识资产；原「团队内唯一」索引 `(team_id, name)` 收紧为 `(name)`。

## 后续迭代（按优先级）

1. **AI 抽取入图**：贴文本/传文件 → 按 schema 抽取实体与关系 → 人工审核入库；可选引用知识库文档 id 做来源溯源（单向引用，不引入模块依赖）
2. **应用绑定与图检索**：`app_agent_config` 增 `graph_ids`（对齐 `wiki_ids`），Agent 检索图谱（text2cypher 起步，只读白名单）回答多跳关系问题
3. connected 图画布渲染；画布编辑模式；结构化导入（CSV/JSON/三元组）；Cypher 控制台
