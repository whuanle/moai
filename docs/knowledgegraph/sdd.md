# 知识图谱模块设计规格（SDD）

> 关联：[SDD](./sdd.md) ｜ [BDD](./bdd.md) ｜ [TDD](./tdd.md) ｜ [SOP](./sop.md) ｜ 上游：[../team/sdd.md](../team/sdd.md)、[../settings/sdd.md](../settings/sdd.md) ｜ 设计：[2026-09-10-knowledge-graph-design.md](../superpowers/specs/2026-09-10-knowledge-graph-design.md)、[2026-09-14-knowledge-graph-memgraph-canvas-design.md](../superpowers/specs/2026-09-14-knowledge-graph-memgraph-canvas-design.md) ｜ 证据：[local-dev/kg-e2e.mjs](../../local-dev/kg-e2e.mjs)

- 日期：2026-09-14（v2）
- 状态：后端 v2 已实现（Memgraph 方言化 + 画布/邻接接口 + 权限收紧 + 名称全局唯一）；前端入口/画布已接线；E2E 待图数据库实例
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
- `GraphDriverProvider`（单例）按 `(uri, username, password, dialect)` 缓存 `IDriver`，变化时重建并异步释放旧实例；首次写入前幂等初始化索引。
- `CypherKnowledgeGraphStore`（scoped）封装全部 Cypher；**单库 + `kgId` 属性隔离多图谱**（Memgraph 多数据库为企业版 3.10+ 特性，社区版 `CREATE DATABASE` 被拒，故不做物理分库，见 D20）。节点属性名统一 `kgId`（v2 前代码曾用 `KnowledgeGraphId`，已统一）。
- connected 会话仅 neo4j 方言按库名路由（`WithDatabase`），memgraph 恒用默认库。
- `IKnowledgeGraphAuthorizer` 统一团队角色判定，并对 connected 图拦截写操作。

## 3. 数据模型

### 3.1 PostgreSQL（只存目录与 schema，不存图数据）

- `kg`：`id / team_id / name(50) / description(255) / template_key(50, null=自定义) / mode(20, 默认 managed) / database(100, 仅 connected) / is_deleted / 审计`
  - 索引 `idx_knowledge_graph_team_id`；partial 唯一 `idx_knowledge_graph_name_live_uindex (name) WHERE is_deleted = 0`（v2 起名称**全平台唯一**，原 `(team_id, name)` 收紧）
- `kg_entity_type` / `kg_relation_type`：同 v1（关系类型带 `source_type_id / target_type_id` 起止约束）。

### 3.2 图库 keyspace

```cypher
(:KgNode { id: <guid>, kgId: <long>, entityTypeId: <long>, name, description })
-[:KG_REL { id: <guid>, kgId: <long>, relationTypeId: <long> }]->
(:KgNode { ... })
```

类型名 / 颜色回 PG 查询，改 schema 零迁移；`kgId` 隔离多图；删节点 `DETACH DELETE` 连带边；删托管图按 `kgId` 清空。模板目录（blank/ops/org/event）代码内置只读，建图时一次性复制（D1~D13 见 v1 设计稿）。

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
| POST | `/{id}/canvas` | 有界子图 `{entityTypeId?, label?, relationTypeId?, keyword?, limit≤500 默认 200}` → `{nodes, edges, truncated}`，边仅返回节点集内部；托管图按 `entityTypeId` 过滤，接入图按 `label` 过滤（v2.1 起接入图开放） |
| GET | `/{id}/nodes/{nodeId}/neighbors?limit=` | 一跳邻接（默认 100，上限 500）→ 同上结构；托管图按节点 id，接入图按 elementId（v2.1 起接入图开放） |
| GET | `/{id}/schema?refresh=` | 接入图 schema 走 Redis 内省缓存（TTL 5 分钟），`refresh=true` 强制重新内省并返回相对基线的 `changes`（v2.1） |
| POST | `/{id}/avatar` | 设置图谱头像 `{objectKey}`（v2.2）：仅 Owner/Admin；objectKey 须为已登记且完成上传的文件（伪造 404）；列表/详情回显 `avatarPath`（前端经 `/static/{key}` 解析） |

均为只读、Member 可用。请求模型实现 `IModelValidator<T>`。

### 5.1 外部开放接口（`/api/external/knowledge-graph`）

- **授权模型（团队级）**：接入应用（access_app）创建即授权其团队的资源访问——应用 token（`UserType.ExternalApp`，携带 `TeamId` claim）对本团队托管图谱**等价团队 Admin**；`AccessAppEntity.AppIds` 白名单已删除。仅接受应用 token（用户/匿名/内部 JWT 一律 401/403）。
- **端点面（21 个）**：`POST list`（仅返回本团队 **managed** 图谱，connected 不对外暴露）、`GET {kgId}/schema`、实体类型/关系类型各 CRUD（3+3）、节点 list/详情/一跳邻接/CRUD/批量（8）、边 list/详情/CRUD/批量（6）。
- **语义**：跨团队或不存在一律 404（不泄露存在性）；connected 图谱写操作 409；批量导入节点/边 ≤200 条/次，**先整批校验后写入，任一条失败整批 400 拒绝**（类型存在、边端点存在于该图谱、超上限校验层拒绝）；删节点 DETACH 级联。
- **Caller 注入模式**：Controller `RequireCaller()` 从 `HttpContext.Items` 取 `ExternalTokenContext` 组装 `ExternalGraphCaller{TeamId, AccessAppId}` 随命令下发；Core 侧 `IExternalKnowledgeGraphAuthorizer.AuthorizeAsync(kgId, teamId, write)` 统一 404/409，Handler 复用 `IKnowledgeGraphStore` 与内部命令同款校验规则（角色门禁替换为外部授权器），场景见 bdd `@KX-01…KX-08`、证据 [kg-external-e2e.mjs](../../local-dev/kg-external-e2e.mjs)。

## 6. 关键流程与校验（Core）

- 能力门禁、建图模板复制事务、接入探活、connected 只读、类型删除引用拦截、节点 / 边起止约束校验均同 v1（见 v1 设计稿 §6）。
- **名称唯一（v2）**：预检 `AnyAsync(x => x.Name == …)` 不再限定团队；数据库约束 `idx_knowledge_graph_name_live_uindex` 兜底 409。
- **图库不可达**：`ServiceUnavailableException` 等统一映射 503「无法连接图数据库」（v1 落 500 的遗留已修复）。
- 画布查询：先取节点子集（`ORDER BY name LIMIT limit+1` 判截断），再取 `s.id IN $ids AND t.id IN $ids` 的内部边（上限 `min(limit*5, 5000)`）。
- **模型属性设置（v2.3）**：实体类型新增属性定义（`knowledge_graph_entity_type.properties` jsonb，数组 `{name,type(string/number/boolean/date),required,description}`，≤50 项、名称唯一 ≤100）；实例属性值以单个 `propsJson` 字符串属性存储在图库 `KgNode`（`SET n.propsJson`），查询时反序列化到节点项 `properties` 字典；实例表单按所选类型的属性定义渲染（number→InputNumber、boolean→Switch、date→DatePicker），必填属性前端校验。
- **接入图动态识别（v2.1）**：外部节点无统一 schema，以 `elementId(n)` 定位、`coalesce(toString(n.name), toString(n.title), toString(n.id), elementId(n))` 启发式取名、首个标签作实体类型（`labels(n)` 经 Memgraph 单标签字符串归一化）；label 过滤值经反引号校验后注入 Cypher。内省结果经 `KnowledgeGraphIntrospectionCache` 缓存：`kg:introspect:{kgId}:{db}`（TTL 5 分钟）+ `kg:introspect:baseline:{kgId}:{db}` 长期基线，新鲜内省时与基线 diff 出新增/消失的标签与关系类型（`changes` 字段）；删图时清理两 key。

## 7. 前端（v2 接线）

- 路由：`/knowledge-graph`（跨团队卡片墙，对齐 wiki 模式）+ `/team/:teamId/kg/:graphId/:section?`（详情）；团队详情独立「知识图谱」分区（`TeamKnowledgeGraphs`，建图 / 接入 / 编辑 / 删除），Member 可见但只读。
- 侧边栏 mainNav 新增「知识图谱」（`ClusterOutlined`）；`/team/*/kg/**` 高亮该入口。
- 详情 section：托管图默认「图览」（`@antv/g6` v5 力导向，类型色板过滤、关键字搜索、点节点一跳展开、截断提示、空态引导）→ 实体 → 关系 → 模型 → 设置；接入图仅模型 / 设置 + 只读徽标。
- Member 只读：实体 / 关系页按 `myRole` 不渲染新增 / 编辑 / 删除入口。

## 8. 关键决策（v2，D14~D21）

完整论证见 [v2 设计稿](../superpowers/specs/2026-09-14-knowledge-graph-memgraph-canvas-design.md#决策)：**D14** 单实现内按 dialect 分支方言差异；**D15** 统一 `Neo4j.Driver` 访问两方言；**D16** 入口对齐 wiki 平级模式；**D17** Member 全只读（v1 放宽条款废除）；**D18** 画布本期只读；**D19** 画布仅托管图；**D20** 单库 + `kgId` 逻辑隔离（Memgraph 多数据库系企业版特性）；**D21** 图谱名称全局唯一。v2.1 新增：**D22** 接入图开放画布/邻接（elementId 定位 + 启发式取名，覆盖“外部服务直写图库、平台只读转接”场景）；**D23** PG 保留目录/权限/约束建模（不做 source of truth 迁移），仅动态内省结果落 Redis 缓存 + 基线 diff；**D24** 属性定义存实体类型 jsonb 列、实例属性值以 `propsJson` 单属性存图库节点（Memgraph/Neo4j 属性值不支持嵌套 map，单 JSON 字符串属性避免键污染且便于整体覆盖）；v2.3 顺带修复 v1 遗留：Update 节点/边/实体类型/关系类型 4 个命令 validator 校验路由字段导致所有编辑操作 400。

## 9. 已知问题 / 下阶段

- 存量开发库若存在旧唯一索引 `(team_id, name)`，需手动 `DROP INDEX` + 按 EF 配置重建（`EnsureCreated` 不做增量演进）；旧设置键 `OPEN_NEO4J/NEO4J_*` 不迁移，超管需在设置页重配一次。
- 托管图名称/类型等混合「路由 + 请求体」绑定的命令，自动模型校验发生在路由回填前，validator 不校验路由字段（与 wiki 等模块约定一致，v1 曾误加规则导致 400，v2 已修复）。
- 接入图画布/邻接依赖 `elementId()`，要求 Memgraph ≥ 2.14（Neo4j 4.x+ 均支持）；外部直写数据的质量与租户隔离由接入方负责。
- 外部接口 schema 计数为逐类型计数（N+1）：类型很多时图库往返次数多，量级可控，暂不合并为单条 Cypher。
- 外部接口「删除类型」与「批量写」存在毫秒级并发竞态（删除检查与批量校验非同一事务）；面向可信调用方可接受。
- 平台存在双错误格式：`ExternalAuthenticationMiddleware` 的 `{error:...}` 与全局管道的 `BusinessValidationResult` 并存（既有行为，外部调用方需两者都兼容）。
- 后续迭代：AI 抽取入图（审核流）、应用绑定 `graph_ids` 与图检索（graph as tool）、画布编辑、结构化导入、Cypher 控制台。
