# 知识图谱模块设计规格（SDD）

> 关联：[SDD](./sdd.md) ｜ [BDD](./bdd.md) ｜ [TDD](./tdd.md) ｜ [SOP](./sop.md) ｜ 上游：[../team/sdd.md](../team/sdd.md)、[../settings/sdd.md](../settings/sdd.md) ｜ 设计：[2026-09-10-knowledge-graph-design.md](../superpowers/specs/2026-09-10-knowledge-graph-design.md) ｜ 证据：[local-dev/kg-e2e.mjs](../../local-dev/kg-e2e.mjs)

- 日期：2026-09-10
- 状态：后端 v1 已实现（托管图谱 + 外部接入图谱）；前端页面、文档抽取、知识库绑定、结构化导入不在本期
- 领域：`src/knowledgegraph/{Shared,Core,Api}`；DDL `asserts/knowledge_graph.sql`；单测 `tests/MoAI.KnowledgeGraph.Tests/`

## 1. 目标

向量检索无法回答多跳关系问题。知识图谱模块用 Neo4j 存实体与关系，交付两类图：

- **托管图谱（`mode=managed`）**：团队可建多个图谱，手动维护 schema（实体类型 / 关系类型）与节点 / 边；建图可选内置模板一次性复制类型。
- **外部接入图谱（`mode=connected`）**：登记同一 Neo4j 实例的某个 database，自动内省标签 / 关系类型 / 属性键，默认只读，不迁移、不修改外部数据。

能力由系统设置开关 `OPEN_NEO4J` 门禁；未开启时建图 / 接入与图操作返回 409。后续迭代见设计稿。

## 2. 架构与模块

三层与 `wiki` 平级、互不依赖：`*.Shared`（Command/Query/Response/Models）→ `*.Core`（Handler、Neo4j 访问、模板目录）→ `*.Api`（`KnowledgeGraphController`）。Core 仅引用 `MoAI.Settings.Shared` 与 `MoAI.Team.Shared` 接口。

- `IKnowledgeGraphSettingsService`（settings 模块）读取 `OPEN_NEO4J / NEO4J_URI / NEO4J_USERNAME / NEO4J_PASSWORD`，不新增 `SystemOptions`。
- `Neo4jDriverProvider`（单例）按 `(uri, username, password)` 缓存 `IDriver`，连接信息变化时重建并异步释放旧实例；首次写入前幂等创建约束与索引。
- `IKnowledgeGraphStore`（scoped）经 provider 取 driver，封装全部 Cypher。
- `IKnowledgeGraphAuthorizer` 统一团队角色判定，并对 connected 图拦截写操作。

## 3. 数据模型

### 3.1 PostgreSQL（只存目录与 schema，不存图数据）

- `kg`：`id / team_id / name(50) / description(255) / template_key(50, null=自定义) / mode(20, 默认 managed) / database(100, 仅 connected) / is_deleted / 审计`
  - 索引 `idx_kg_team_id`；partial 唯一 `(team_id, name) WHERE is_deleted = 0`
- `kg_entity_type`：`id / kg_id / name(50) / color(20) / description(255) / sort / is_deleted / 审计`；partial 唯一 `(kg_id, name) WHERE is_deleted = 0`
- `kg_relation_type`：`id / kg_id / name(50) / color(20) / description(255) / source_type_id(可空=任意) / target_type_id(可空=任意) / sort / is_deleted / 审计`；partial 唯一 `(kg_id, name) WHERE is_deleted = 0`

软删除由框架注入、查询过滤器统一过滤；不建物理外键（同脚手架约定）。

### 3.2 模板（代码内置只读，不进库）

`KnowledgeGraphTemplates`：`blank`（空白）、`ops`（服务/人员/项目 + 维护/依赖）、`org`（人员/部门/公司 + 任职/隶属）、`event`（事件/时间/人物 + 参与/先后）。建图带 `templateKey` 时在 Core 内把类型批量写入 `kg_entity_type / kg_relation_type`，关系类型按名称映射到落库后的实体类型 id（D5）。

### 3.3 Neo4j keyspace（v1 固定标签 / 关系类型，避免动态拼 Cypher）

```cypher
(:KgNode { id: <guid>, kgId: <long>, entityTypeId: <long>, name, description })
-[:KG_REL { id: <guid>, kgId: <long>, relationTypeId: <long> }]->
(:KgNode { ... })
```

- 实体"类型"靠 `entityTypeId` 回 PG 查名称/颜色，不写进标签 → 改类型名零迁移（D3）。
- `kgId` 挂每个节点与边，多图谱共用一库天然隔离（D4）。
- 幂等初始化：`kg_node_id_unique`（`KgNode.id` 唯一）+ `kg_node_kg_name`（`kgId, name` 索引）。
- 删节点 `DETACH DELETE` 连带其边；删托管图谱按 `kgId` 清空节点与边（D9）。

### 3.4 外部接入（connected，只读）

- 直接读外部 database 中对方自己的节点 / 关系（任意标签与属性），不套 `:KgNode`（D13）。
- 登记探活 `CALL db.labels()` 成功即可接入，失败 400；内省 `db.labels() / db.relationshipTypes() / db.propertyKeys()` 并附计数（尽力而为）。
- 只读：不提供节点 / 边 / schema 写操作；删除仅软删平台登记，绝不对 database 执行写删。

## 4. 权限（复用 Team 角色，Handler 判定）

| 操作 | Owner/Admin | Member | 非成员 |
|---|---|---|---|
| 建图 / 接入 / 改图 / 删图 | ✅ | 403 | 404 |
| 列表 / 详情 | ✅ | ✅ | 404 |
| 实体类型、关系类型增删改（仅托管图） | ✅ | 403 | 404 |
| 节点 / 边增删改查（仅托管图） | ✅ | ✅ | 404 |
| schema 内省（接入图） | ✅ | ✅ | 404 |

列表 / 详情响应携带 `myRole` 与能力开关 `enabled`；角色判定注入 `ITeamService`（D7）。

## 5. API（`KnowledgeGraphController`，路由 `/api/knowledge-graph`）

| 方法 | 路由 | 说明 |
|---|---|---|
| POST | `/` | 建图 / 接入 `{teamId, name, description?, mode, templateKey?|database?}` |
| GET | `/list?teamId=` | 团队图谱列表（含 myRole、enabled、mode、readOnly） |
| GET | `/{id}` | 详情（含 myRole、templateKey、mode、database、readOnly） |
| PUT | `/{id}` | 改名称 / 简介 |
| DELETE | `/{id}` | 托管：软删 + 清空 Neo4j；接入：仅软删登记 |
| GET | `/templates` | 内置模板目录 |
| GET | `/{id}/schema` | 托管：实体/关系类型；接入：内省标签/关系/属性键 + 计数 |
| POST/PUT/DELETE | `/{id}/entity-types[/{typeId}]` | 实体类型增 / 改 / 删（仅托管） |
| POST/PUT/DELETE | `/{id}/relation-types[/{typeId}]` | 关系类型增 / 改 / 删（仅托管） |
| POST | `/{id}/nodes/list` | 节点分页（按类型 / 名称过滤） |
| POST/GET/PUT/DELETE | `/{id}/nodes[/{nodeId}]` | 节点增 / 详情 / 改 / 删（级联边） |
| POST | `/{id}/edges/list` | 边分页（按关系类型 / 端点过滤） |
| POST/GET/PUT/DELETE | `/{id}/edges[/{edgeId}]` | 边增 / 详情 / 改 / 删 |

请求模型实现 `IModelValidator<T>`；响应列表 DTO 携带审计字段时继承 `AuditsInfo`。

## 6. 关键流程与校验（Core）

- **能力门禁**：建图 / 接入与 schema、节点、边写操作前读设置，`Enabled=false` → 409；`Enabled` 但 `Uri` 为空 → 409。
- **建图（managed）**：团队 Admin+；同团队未删除同名 409；带模板则复制类型；模板 key 非法 400。
- **接入（connected）**：团队 Admin+；必填 `database`、禁止 `templateKey`；探活失败 400；同名 409；`database` 去空格后落库。
- **只读约束**：`mode=connected` 时 schema CRUD、节点 / 边 CRUD 经 `AuthorizeManagedAsync` 一律 409。
- **类型删除**：仍有节点用其 `entityTypeId` / 边用其 `relationTypeId`，或被关系类型引用 → 409；否则软删。
- **建节点**：`entityTypeId` 必须属于该图谱，否则 400。**建边**：关系类型、两端节点均属该图谱；关系类型设了 `source_type_id / target_type_id` 时端点类型必须匹配，否则 400。
- 图数据全在 Neo4j，不涉及 Redis 用户态，无需 `RemoveUserStateAsync`。

## 7. 关键决策

完整论证见[设计稿](../superpowers/specs/2026-09-10-knowledge-graph-design.md#决策)；要点：**D1** 独立模块（与知识库平级，团队级多个）；**D2** 元数据存 PG、图数据存 Neo4j 分治；**D3** 固定标签 + `typeId` 属性，改 schema 零迁移；**D4** `kgId` 隔离多图共用一库；**D5** 模板一次性复制；**D6** 复用设置能力、`OPEN_NEO4J` 门禁；**D7** 权限沿用团队角色（schema 管理 Admin+，图内容放开成员）；**D8** v1 不做自定义属性字段；**D9** 删托管图谱同步清 Neo4j；**D10** 知识库绑定与文档抽取延后；**D11** `mode` 区分双来源；**D12** 接入限定同实例 database、不引入第二套凭据；**D13** 接入只读且不迁移。

## 8. 已知问题 / 下阶段

- 前端 `/kg`、工作台与团队详情 tab 未落地（设计稿 §前端）。
- Neo4j 不可达目前落入全局 500（设计稿期望 503 映射，尚未实现）。
- 接入图节点级浏览、接入图可写开关、自定义属性、文档 LLM 抽取、知识库绑定、结构化导入、画布与检索均为后续迭代。
