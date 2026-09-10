# 知识图谱模块设计（v1：独立图谱 + 手动维护 + 外部接入）

- 日期：2026-09-10
- 状态：待评审
- 关联：[设置 SDD](../../settings/sdd.md) ｜ [知识库 SDD](../../wiki/sdd.md) ｜ [CQRS 规范](../../cqrs-conventions.md) ｜ [前端规范](../../../ui/docs/frontend-conventions.md) ｜ [数据库脚手架](../../database-scaffold/sdd.md)

## 背景与目标

向量检索回答不了关系问题。例如"支付接口超时多久、由谁维护、影响哪个项目"：向量库能召回相关片段，但"由谁维护、影响哪个项目"必须把 `PaymentService` 与"王工""结算平台"之间的关联存下来才能答出。用 Neo4j 存实体与关系，把这类多跳关系查询做便宜。

v1 交付一个**独立的知识图谱模块**，图谱有两种来源：

- **平台托管（managed）**：团队可创建多个图谱，用户在工作台手动维护节点与边；创建时可套用内置模板，也可完全自定义 schema。
- **外部接入（connected）**：业务侧已有 Neo4j 图谱（同一实例的某个数据库），平台只登记接入 + 自动内省 schema，默认只读，供后续 AI 应用消费。

文档 LLM 自动抽取、绑定知识库经 MQ 自动生成、结构化文件导入均为后续迭代。

## 范围

**包含：**
- 知识图谱领域：团队级、可建多个、增删改查
- **托管图谱**：schema（实体类型、关系类型）增删改；内置模板目录（选用即复制类型到图谱）；节点、边的手动增删改查（数据仅存 Neo4j）
- **接入图谱**：登记外部 Neo4j 数据库（同一实例）+ 自动内省标签 / 关系类型 / 属性键，只读，不迁移、不修改外部数据
- 前端：`/kg` 卡片墙、团队详情「知识图谱」tab、工作台（托管：实体 / 关系 / 模型 / 设置；接入：模型 / 设置）
- 能力门禁：`OPEN_NEO4J` 未开启时不可建图/接入，图谱内容操作不可用

**不包含（后续迭代）：**
- 文档 LLM 抽取三元组入图
- 绑定知识库 + 向量化完成 MQ 自动生成
- 结构化文件导入（CSV / Excel / JSON / 三元组）、Cypher 控制台、开放 API
- 公开图谱、图谱向量化与检索、节点/边自定义属性字段
- 画布（图可视化）视图

## 架构与模块

新增 `src/knowledgegraph`，三层 `MoAI.KnowledgeGraph.Shared / Core / Api`，与 `wiki` 平级、不互相依赖。

- `*.Shared`：Command / Query / Response / Models（含模板模型）
- `*.Core`：Handler、`IKnowledgeGraphStore`（Neo4j 访问封装）、`Neo4jDriverProvider`、模板目录；引用 `MoAI.Settings.Shared` 与 `MoAI.Team.Shared`
- `*.Api`：`KnowledgeGraphController`

**配置来源**：不新增 `SystemOptions`。连接与开关经 `IKnowledgeGraphSettingsService.GetAsync()` 读取（`OPEN_NEO4J` / `NEO4J_URI` / `NEO4J_USERNAME` / `NEO4J_PASSWORD`）。

**驱动生命周期**：设置项无缓存、下一个读请求即生效，故不注册静态单例 driver。`Neo4jDriverProvider`（单例）按 `(uri, username, password)` 缓存 `IDriver`，连接信息变化时重建并 `Dispose` 旧实例；`IKnowledgeGraphStore`（scoped）每次经 provider 取 driver。

**本地环境**：Neo4j 实例由开发者提供；在 docker compose 增补可选 `neo4j` 服务并在 `local-dev` 说明，作为本地默认。生产由超级管理员在系统设置页配置连接。

## 数据模型

### PostgreSQL（只存目录与 schema，不存图数据）

DDL 新增 `asserts/knowledge_graph.sql`，实体与 Configuration 放在 `src/database`，保持与脚手架一致（审计五件套、boolean 软删除、partial 唯一索引、不建物理外键）。

- `kg`（图谱）
  - `id / team_id / name(varchar50) / description(varchar255) / template_key(varchar50, null=自定义) / mode(varchar20, managed|connected) / database(varchar100, null) / is_deleted / 审计`
  - `mode` 默认 `managed`；`database` 仅 `connected` 必填（同实例数据库名），`managed` 为空
  - 索引 `idx_kg_team_id`
  - partial 唯一 `(team_id, name) WHERE is_deleted = false`
- `kg_entity_type`（实体类型）
  - `id / kg_id / name(varchar50) / color(varchar20) / description(varchar255) / sort(int) / is_deleted / 审计`
  - partial 唯一 `(kg_id, name) WHERE is_deleted = false`
- `kg_relation_type`（关系类型）
  - `id / kg_id / name(varchar50) / color(varchar20) / description(varchar255) / source_type_id(bigint, null=任意) / target_type_id(bigint, null=任意) / sort(int) / is_deleted / 审计`
  - partial 唯一 `(kg_id, name) WHERE is_deleted = false`

### 模板

模板不进库，代码内置只读目录 `KnowledgeGraphTemplates`（key、名称、描述、实体类型列表、关系类型列表）。示例：
- `ops`：服务 / 人员 / 项目 + 维护 / 依赖
- `org`：人员 / 部门 / 公司 + 任职 / 隶属
- `event`：事件 / 时间 / 人物 + 参与 / 先后
- `blank`：空白（无类型，自行定义）

建图时若带 `templateKey`，在 Core 内把模板类型批量写入 `kg_entity_type` / `kg_relation_type` 并建立类型间关系约束（用名称映射为落库后的 type id）；`blank`/缺省则不写。模板不随图谱变更同步（一次性复制）。

### Neo4j（v1 固定标签 / 关系类型，避免动态拼 Cypher）

```cypher
(:KgNode {
   id: <string guid>, kgId: <long>, entityTypeId: <long>,
   name: <string>, description: <string>
})
-[:KG_REL {
   id: <string guid>, kgId: <long>, relationTypeId: <long>
}]->
(:KgNode { ... })
```

- 实体"类型"靠 `entityTypeId` 回 PG 查名称/颜色，不写进标签 → 改类型名零迁移
- `kgId` 挂每个节点与边：多图谱共用一个库，天然隔离
- 初始化（幂等）：
  - `CREATE CONSTRAINT kg_node_id_unique IF NOT EXISTS FOR (n:KgNode) REQUIRE n.id IS UNIQUE`
  - `CREATE INDEX kg_node_kg_name IF NOT EXISTS FOR (n:KgNode) ON (n.kgId, n.name)`
- 删图谱：`MATCH (n:KgNode {kgId:$kgId}) DETACH DELETE n`
- 删节点：`MATCH (n:KgNode {kgId:$kgId, id:$id}) DETACH DELETE n`（连同其边）

### 接入图谱（connected，只读）

接入图直接读外部数据库里对方自己的节点/关系（任意标签/属性），不套 `:KgNode` 结构：

- 访问方式：`driver.AsyncSession(b => b.WithDatabase(database))`，`database` 为该图谱登记的库名
- 登记探活：`CALL db.labels()` 成功即视为可接入；失败 → 400「数据库不存在或无法访问」
- schema 内省：
  - `CALL db.labels()` → 标签（实体类型候选）
  - `CALL db.relationshipTypes()` → 关系类型
  - `CALL db.propertyKeys()` → 属性键
  - 每个标签/关系类型附带计数（`MATCH (n:`\`label\``) RETURN count(n)`；标签名反引号转义，含反引号的标签跳过），计数为尽力而为
- 只读：不提供节点/边/schema 写操作
- 删除：只软删平台登记记录，绝不对 `database` 执行任何写/删

## 权限（复用 Team 角色，Handler 判定）

| 操作 | Owner/Admin | Member | 非成员 |
|---|---|---|---|
| 建图 / 接入 / 改图 / 删图 | ✅ | 403 | 404 |
| 列表 / 详情 | ✅ | ✅ | 404 |
| 实体类型、关系类型增删改（仅托管图） | ✅ | 403 | 404 |
| 节点 / 边增删改查（仅托管图） | ✅ | ✅ | 404 |
| schema 内省（接入图） | ✅ | ✅ | 404 |

- 列表/详情响应携带 `myRole`
- 角色判定注入 `MoAI.Team.Shared` 的 `ITeamService`；Core 只引用 Team 接口项目
- v1 无公开图谱

## API（`KnowledgeGraphController`，路由 `knowledge-graph`）

| 方法 | 路由 | 说明 |
|---|---|---|
| POST | `/api/knowledge-graph` | 建图/接入 `{teamId, name, description?, mode, templateKey? \| database?}` |
| GET | `/api/knowledge-graph/list?teamId=` | 列表（含 myRole、`enabled` 能力开关、mode） |
| GET | `/api/knowledge-graph/{id}` | 详情（含 myRole、templateKey、mode、database、`readOnly`） |
| PUT | `/api/knowledge-graph/{id}` | 改名称/简介 |
| DELETE | `/api/knowledge-graph/{id}` | 托管：软删 + 清空 Neo4j；接入：仅软删登记，不动外部库 |
| GET | `/api/knowledge-graph/templates` | 内置模板目录 |
| GET | `/api/knowledge-graph/{id}/schema` | 托管：实体类型 + 关系类型；接入：内省标签/关系类型/属性键 + 计数 |
| POST | `/api/knowledge-graph/{id}/entity-types` | 新增实体类型 |
| PUT | `/api/knowledge-graph/{id}/entity-types/{typeId}` | 改实体类型 |
| DELETE | `/api/knowledge-graph/{id}/entity-types/{typeId}` | 删实体类型 |
| POST | `/api/knowledge-graph/{id}/relation-types` | 新增关系类型 |
| PUT | `/api/knowledge-graph/{id}/relation-types/{typeId}` | 改关系类型 |
| DELETE | `/api/knowledge-graph/{id}/relation-types/{typeId}` | 删关系类型 |
| POST | `/api/knowledge-graph/{id}/nodes/list` | 节点分页（按类型 / 名称过滤） |
| POST | `/api/knowledge-graph/{id}/nodes` | 新增节点 |
| GET | `/api/knowledge-graph/{id}/nodes/{nodeId}` | 节点详情 |
| PUT | `/api/knowledge-graph/{id}/nodes/{nodeId}` | 改节点 |
| DELETE | `/api/knowledge-graph/{id}/nodes/{nodeId}` | 删节点（级联边） |
| POST | `/api/knowledge-graph/{id}/edges/list` | 边分页（按关系类型 / 端点过滤） |
| POST | `/api/knowledge-graph/{id}/edges` | 新增边 |
| GET | `/api/knowledge-graph/{id}/edges/{edgeId}` | 边详情 |
| PUT | `/api/knowledge-graph/{id}/edges/{edgeId}` | 改边 |
| DELETE | `/api/knowledge-graph/{id}/edges/{edgeId}` | 删边 |

请求模型实现 `IModelValidator<T>`；枚举带 `JsonPropertyName`；响应列表 DTO 继承 `AuditsInfo`（若含审计）并用 `IUserInfoFillService.FillAsync` 填充人名。列表/详情带 `enabled`，让非 root 的前端也能据能力开关调整 UI（系统设置读取接口仅 root，不能用于此判断）。

## 关键流程与校验（Core，跨存储校验在 `IKnowledgeGraphStore`）

- **能力门禁**：建图/接入与所有节点/边操作前读设置；`Enabled=false` → 409「未开启知识图谱能力」
- **建图（managed）**：团队角色 Admin+；同团队未删除同名 409；带模板则复制类型
- **接入（connected）**：团队角色 Admin+；必填 `database`、禁止 `templateKey`；探活失败 400；同名 409
- **只读约束**：`mode=connected` 时 schema CRUD、节点/边 CRUD 一律 409「外部接入图谱为只读」
- **实体类型 / 关系类型删除**（仅 managed）：先查 Neo4j，仍有节点用 `entityTypeId`（或边用 `relationTypeId`）→ 409 拒绝；否则软删
- **建节点**：`entityTypeId` 必须属于该图谱
- **建边**：`relationTypeId` 属于该图谱；起止节点存在于该图谱；关系类型若设 `source_type_id`/`target_type_id`，则端点类型必须匹配，否则 400
- **删节点**：`DETACH DELETE`，连带删除其边
- **删图谱**：managed → PG 软删 + 同步清 Neo4j；connected → 仅 PG 软删，绝不写外部库
- 节点/边数据全在 Neo4j，不涉及 Redis 用户态，无需 `RemoveUserStateAsync`

## 前端

路由对齐 wiki：
- `/kg`：我加入的所有团队的知识图谱**卡片墙**，只读聚合（`getMyTeams()` → 逐团队列表合并），不提供管理入口
- `/team/:teamId/kg/:graphId/:section?`：工作台；`managed` 的 `section ∈ entities(默认) | relations | schema | settings`，`connected` 为 `schema(默认) | settings`
- 团队详情「知识图谱」tab：新建/接入 / 改 / 删（仅 Owner/Admin），数据源 `myRole` 判定
- 建图/接入弹窗：先选「平台托管」或「接入已有」；托管填名称/简介/模板，接入填名称/库名
- 工作台（managed）：左侧菜单（实体 / 关系 / 模型 / 设置）；实体与关系为两个列表页（关系行显示 `起点 → 关系 → 终点`）；「模型」管理实体类型与关系类型（Admin+ 可编辑）
- 工作台（connected）：左侧菜单（模型 / 设置），顶部"外部接入 · 只读"提示；「模型」只读展示内省出的标签 / 关系类型 / 属性键与计数；删除时提示"仅从平台移除该接入，不影响外部数据"
- 画布后续在实体页加「列表 / 画布」切换，v1 不放

约束：全部走 `@/design-system`（Page / DataTable / Form / Modal / Popconfirm）；危险操作 `Popconfirm`；文案走 `t()`，zh-CN 与 en-US 同步；时间用 `formatDateTime()`；封装放 `ui/src/api/knowledgeGraph.ts`，页面只调封装层；Kiota 生成物禁手改。

## 失败处理

- `OPEN_NEO4J=false`：`/kg` 卡片墙与工作台照常可用（元数据在 PG，可看），但「新建/接入」禁用并提示，节点/边操作返回 409；建图/接入请求同样 409
- Neo4j 不可达 / 连接信息错误：存储层异常映射为 503，提示检查系统设置
- 接入库不存在/无权限：探活失败 → 400「数据库不存在或无法访问」
- 模板类型复制失败：整图创建回滚（同一事务写 PG）
- 前端列表/详情加载失败保留页面骨架并提示，可重试

## 验证

- 后端：`dotnet build src/MoAI/MoAI.csproj` 0 error；单测覆盖 Handler 校验（类型归属、关系类型约束、删除引用拦截、能力门禁）
- E2E：`local-dev/kg-e2e.mjs`，覆盖 建图（含模板）→ schema CRUD → 节点/边 CRUD → 约束校验 → 删类型引用拦截 → 删图清空；接入场景覆盖 接入探活 → 内省 schema → 只读拒绝（需后端 + Neo4j + `OPEN_NEO4J=true`）
- 前端：`npm run typecheck && npm run lint && npm run test`，页面组件测试写在同目录 `__tests__/`
- 文档：新增 `docs/knowledgegraph/{sdd,bdd,tdd,sop}.md`，场景编号 `KG-S<n>`

## 决策

- **D1 独立模块**：知识图谱与知识库平级，团队级可建多个；不寄生在知识库内
- **D2 元数据 / 图数据分治**：schema 与目录存 PostgreSQL，节点/边只存 Neo4j，避免双写
- **D3 固定标签 + typeId 属性**：不用动态标签 / 动态关系类型，用户改 schema 不需迁移图
- **D4 `kgId` 隔离**：多图谱共用 Neo4j 库，靠 `kgId` 属性隔离与清理
- **D5 模板一次性复制**：模板只读内置，创建时复制类型到图谱，之后各自演化
- **D6 复用设置能力**：不新增 `SystemOptions`，经 `IKnowledgeGraphSettingsService` 感知开关与连接；`OPEN_NEO4J=false` 时禁用建图与图操作
- **D7 权限沿用团队角色**：图谱与 schema 管理为 Admin+，图内容维护放开到团队成员
- **D8 v1 不做自定义属性字段**：节点/边只有名称 + 描述，属性字段后续迭代
- **D9 删图谱同步清理 Neo4j**：v1 直接同步；图量大后改异步任务
- **D10 绑定知识库与文档抽取延后**：向量化完成经 MQ 触发图谱生成的链路不在本期
- **D11 双来源图谱**：`mode=managed`（平台托管，可维护）与 `mode=connected`（外部接入，只读）；同一张 `kg` 表用 `mode` 区分
- **D12 接入按同实例数据库名**：外部图限定为系统设置那台 Neo4j 的某个 database，不引入第二套连接凭据；不区分社区/企业版
- **D13 接入只读且不迁移**：平台不修改外部数据、不强制其套 `:KgNode` 结构；v1 仅登记 + 内省 schema（标签/关系类型/属性键 + 计数），节点级浏览见后续迭代

## 后续迭代

1. 接入图只读浏览：按标签列节点、通用属性面板、关系浏览
2. 接入图可选"可写"开关（复用托管图 CRUD 适配任意结构）
3. 节点/边自定义属性字段与属性 schema
4. 文档 LLM 抽取三元组入图（模板/自定义 schema 约束下抽取）
5. 绑定知识库：向量化完成事件（`Maomi.MQ`）→ 图谱 consumer 增量生成
6. 结构化文件导入（CSV / Excel / JSON、三元组、字段映射）
7. 画布可视化视图与图查询（Cypher / 自然语言）
8. 图谱检索与接入 Agent Framework（`AIContextProvider`）
