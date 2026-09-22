# 设计：知识图谱 Text2Cypher 查图动态插件（`kg_cypher_query`）

> 日期：2026-09-21 ｜ 状态：已评审通过（用户确认方案 A） ｜ 后续：writing-plans 出实现计划
> 关联模块：`src/aiplugin`（动态插件）、`src/knowledgegraph`（图存储）、团队插件（实例创建）

## 1. 背景与动机

知识图谱模块现状是「只进不出」：建图（模板/手动）、AI 导入文件抽取、外部开放 API（21 端点）齐备，但 `src/chat`、`src/wiki`、工作流节点均未消费图数据，图谱价值停留在独立管理页面。

业界（Neo4j Labs MCP、Memgraph Text2Cypher 原子管道、Zep/Graphiti、RAGFlow GraphRAG 开关）的主流玩法中，**「把图查询暴露为 Agent 工具，让模型用自然语言即席查图」是性价比最高、复用现有插件/审批体系最容易的一种**。本设计落地该玩法：新增动态插件模板 `kg_cypher_query`，Agent 应用的对话模型直接生成只读 Cypher 查询知识图谱。

### 业界参照（2026-09 调研结论）

- **Neo4j Labs `mcp-neo4j-cypher`**：schema 作为工具描述喂给 LLM，Agent 即席写 Cypher，只读守卫防写操作——本设计的同款形态。
- **Memgraph Text2Cypher 原子管道**：插件内部调模型做 NL→Cypher（本设计列为后续扩展，见方案 B）。
- **Zep/Graphiti**：对话记忆图化（远期方向，不在本次范围）。
- **RAGFlow/WeKnora**：知识库 GraphRAG 检索增强（独立方向，见「非目标」）。

MoAI 已有的近期先行例：`postgres_query`/`mysql_query` 动态插件模板——连接配置 + `SqlReadOnlyGuard` 只读校验 + 行数截断（`SqlResultReader`），本插件完全照抄该骨架。

## 2. 方案选型（已定）

Cypher 由谁生成，三案对比：

| 方案 | 机制 | 优点 | 缺点 |
|---|---|---|---|
| **A：对话原生（已选）** | 插件只做只读执行器，`{cypher, params}` 由对话模型作为工具参数直接写出 | 零新增 LLM 链路；模型/重试/审批全复用对话循环；Agent 对话与工作流 agentApp 节点通吃 | 依赖对话模型写 Cypher 的能力；工作流普通插件节点不可用 |
| B：内置生成管道 | 请求 `{question}`，插件内部调 AI 补全服务生成 Cypher（错误回喂重试一次） | 工作流普通节点可用；对弱模型友好 | 插件需解析模型配置；多一跳延迟/成本；与对话循环能力重复 |
| C：A+B 混合 | 同一模板支持两种调用姿势 | — | 首版复杂度翻倍；DI 环风险面大 |

**结论：v1 做方案 A。** 若工作流普通节点出现真实诉求，方案 B 可作为同一模板的扩展（或第二模板）后补，A 的守卫与结果读取层原样复用。

## 3. 目标与非目标

**目标**

1. 团队管理员将本团队的**托管图或接入图**绑定为插件实例（仅存 `kgId`，不存连接串）。
2. Agent 应用勾选该插件后，对话模型可：`schema:true` 查图自描述 → 写只读 Cypher → 拿到结果表格继续推理。
3. 工作流中经 **agentApp 节点**（Agent 应用节点）同样可用（复用 Agent 链路，无需新工作流节点）。
4. 安全：只读强制、`$kgId` 跨图隔离、行数封顶、超时、团队归属校验、沿用现有插件审批机制。

**非目标（v1 不做）**

- 写操作（节点/边增删改）——写入与审核流是独立话题；
- 插件内部调模型（方案 B）；
- 社区检测/社区摘要/global search/图算法；
- 工作流普通插件节点直接问自然语言；
- GraphRAG 向量检索融合（实体向量化/entity linking——知识图谱模块 SDD v2.8 已列为下阶段独立迭代）。

## 4. 总体架构与数据流

```
Agent 对话循环（对话模型）
   │ ① call_tool: {"cypher":"MATCH (n:KgNode {kgId:$kgId})...","params":{}}
   ▼
PluginAppToolProvider → PluginExecutor（每次调用独立 DI 作用域）
   ▼
KgCypherQueryPlugin（新模板，src/aiplugin/MoAI.AIPlugin.Dynamic/Plugins/）
   │ ② InitAsync(config)：校验 kgId 绑定与参数范围
   │ ③ RunAsync：CypherReadOnlyGuard 校验 → IKgCypherAccessService
   ▼
KnowledgeGraph.Core 新服务 KgCypherAccessService
   │ ④ 按 kgId 解析：托管图 → GraphDriverProvider（平台 Bolt）
   │                接入图 → kg 实体落库的连接信息（外部 Bolt）
   │ ⑤ 带超时事务执行 → 行数封顶 → 结果表格化
   ▼
{columns, rows, truncated} → 工具文本 → 模型继续推理/回答
```

**依赖方向（无环验证过）**

- 接口 `IKgCypherAccessService` 定义在 `MoAI.KnowledgeGraph.Shared`，实现在 `MoAI.KnowledgeGraph.Core`（随模块注册进宿主 DI）。
- `MoAI.AIPlugin.Dynamic` **只新增引用 `MoAI.KnowledgeGraph.Shared`**。现状：AIPlugin 与 KnowledgeGraph 互不引用；KG Core 仅依赖 Settings/Team 接口，故此方向无循环依赖。
- 插件运行期通过 DI 解析实现（`PluginExecutor` 独立作用域，无 `IAppToolProvider` 式构造环风险）。

## 5. 插件契约

### 5.1 Config（实例配置，存 `plugin_dynamics.ConfigJson`）

```json
{ "kgId": 123, "timeoutSeconds": 30, "maxRows": 200 }
```

| 字段 | 类型 | 约束 | 说明 |
|---|---|---|---|
| `kgId` | long | > 0；实例保存时校验图谱存在且属于本团队 | 绑定的知识图谱（托管或接入） |
| `timeoutSeconds` | int | 1–300，默认 30 | 单次查询超时 |
| `maxRows` | int | 1–1000，默认 200 | 返回行数上限，超出截断 |

不存连接串：托管图连接来自平台配置（`GraphDriverProvider`），接入图连接已在 `kg` 实体落库。信任模型与 `postgres_query` 一致——实例由团队管理员创建，配置即授权。

### 5.2 Request（LLM 写的工具参数，两种模式）

```json
// 查询模式
{ "cypher": "MATCH (n:KgNode {kgId: $kgId}) WHERE n.name CONTAINS $kw RETURN n.name, n.description LIMIT 20",
  "params": { "kw": "仓库" } }
// 自描述模式
{ "schema": true }
```

- 请求 JSON 反序列化沿用 `PluginExecutor` 现约定（大小写不敏感、容注释）。
- 查询模式：`cypher` 必填，`params` 可选对象；`$kgId` 由插件自动注入参数，LLM 不可伪造（见 §7）。
- 自描述模式：`schema:true` 时忽略 `cypher`，返回 §6 的图摘要。

### 5.3 Response

```json
// 查询模式
{ "columns": ["n.name", "n.description"], "rows": [["A仓", "华东主仓"]], "truncated": false }
// 自描述模式
{ "dialect": "memgraph", "graphType": "managed",
  "entityTypes":  [ { "name": "仓库", "properties": ["name", "location"] } ],
  "relationTypes": [ { "name": "存放于", "from": "货物", "to": "仓库" } ],
  "sampleNodes":  [ { "type": "仓库", "names": ["A仓", "B仓"] } ],
  "usage": "只读查询。托管图节点标签为 KgNode、边类型为 KG_REL；所有查询必须用 {kgId: $kgId} 过滤，$kgId 已自动注入，请勿自赋值。" }
```

节点标签事实（照 `CypherKnowledgeGraphStore` 现状写进 usage）：托管图统一 `(:KgNode {id, kgId, entityTypeId, name, description, propsJson})` + `[:KG_REL {id, kgId, relationTypeId}]`；接入图为外部原生 label/关系类型（无 `kgId` 属性，整库即图）。

## 6. Schema 摘要生成（自描述数据源）

| 图类型 | 实体/关系类型来源 | 采样来源 |
|---|---|---|
| 托管图 | PG `knowledge_graph_entity_type`/`knowledge_graph_relation_type`（含属性定义 jsonb、起止类型约束） | 图库按 `entityTypeId` 各采样 ≤3 个节点名 |
| 接入图 | 内省缓存 `IKnowledgeGraphIntrospectionCache`（labels + 关系类型） | 各 label 采样 ≤3 个节点名 |

- 摘要即拼即用，不落缓存（管理页改 schema 后自然生效）。
- 采样单类型查询带 `LIMIT 3`，总耗时可接受；摘要总长度设上限（如 8KB），超限截断并提示用更精确的 Cypher。

## 7. 安全设计

### 7.1 CypherReadOnlyGuard（新类，仿 `SqlReadOnlyGuard`）

- 黑名单关键字（大小写不敏感）：`CREATE`、`MERGE`、`DELETE`、`DETACH`、`SET`、`REMOVE`、`LOAD CSV`、`FOREACH`、`CALL`、`DROP`。
  实现语义澄清：守卫对注释与字符串字面量**先剥除后扫描**——注释/字面量内的黑名单词不触发拒绝，与 `SqlReadOnlyGuard` 同构；`$kgId` 门禁与结果侧归属校验由 KG 访问服务承担。
- 空查询、超长查询（> 8000 字符）直接拒绝。
- 命中即抛 `BusinessException(400, 教学式错误信息)`。

### 7.2 `$kgId` 强制隔离（托管图核心）

多图共用一个 Bolt 库，靠 `kgId` 属性逻辑隔离——LLM 漏写过滤会跨图泄数据。

- 守卫要求查询文本必须包含 `$kgId` 占位符，缺失则报错并在错误信息里教学：`查询必须包含 {kgId: $kgId} 过滤，例如 MATCH (n:KgNode {kgId: $kgId}) ...，请修正后重试`；
- 执行时插件把 `$kgId` 参数注入查询参数（与用户 `params` 合并，用户传 `kgId` 键则覆盖丢弃）；
- 模型在对话循环里看到错误自然重写——这是方案 A 的纠错回路，无需额外重试代码；
- 接入图不要求（整库即图，天然隔离）。

### 7.3 行数封顶与超时

- 结果行数超 `maxRows` 截断，`truncated: true`（仿 `SqlResultReader`）；单单元格值序列化超长（如 > 2000 字符）截断加省略标记。
- 查询经 Neo4j.Driver 事务超时（`SessionConfig`/`TransactionConfig` 的 `WithTransactionConfiguration`，Memgraph 兼容该协议字段）执行，超时抛错不挂会话；若某方言字段不生效，回退为插件侧 `CancellationToken` 硬超时。

### 7.4 权限链

1. 实例创建：仅团队管理员（现有 `POST /team/{teamId}/plugin/dynamic` 门禁不变）；
2. **新增校验**：保存实例时校验 `kgId` 对应图谱存在且 `TeamId` 等于路径团队（越团队 403）；
3. 运行期：沿用插件现有审批机制（approval 模式下插件类工具需人工批准或白名单放行，`AppToolContextProvider` 现行为，不改动）；
4. 接入图对外本就只读语义（KX 既有约定），经本插件同样只读。

## 8. 团队侧与前端

- 团队插件创建弹窗（`/team/{teamId}/plugin/dynamic` 对应页面）针对模板 `kg_cypher_query` 增加「绑定图谱」下拉：列本团队托管图 + 接入图（现有图谱列表 API）。
- 选中图谱后**前端自动预填工具名称与描述**：从图谱 schema API 拉实体/关系类型，拼一段「这张图有什么、能问什么、$kgId 怎么用」的说明。这是模型写对 Cypher 的第一喂养位；`schema:true` 自描述兜底 schema 漂移。
- 后端不做描述强校验（保留管理员手写自由度）；改动集中在团队插件创建页一个表单项 + 一个预填函数。

## 9. 错误处理

全部走现有约定，无新增机制：

- 插件抛 `BusinessException(400, msg)` → `PluginExecutor` 归一化 `{Success=false, Error}` → 对话中以 `{success:false,error}` 文本回给模型，不中断会话；
- 错误信息一律教学式（缺 `$kgId` / 写语句被拒 / 超时 / 空结果提示「可先 `schema:true` 查看图结构」）。

## 10. 测试与验收

### 10.1 单测（挂现有 tests/ 工程）

`CypherReadOnlyGuard`：黑名单关键字逐个、注释内关键字、大小写/全角绕过、超长、空串；缺 `$kgId`（托管图）判定；Config 校验边界。

### 10.2 E2E（新建 `local-dev/kg-text2cypher-e2e.mjs`，场景缩写 **KT**，依赖 kg-e2e 同款 Memgraph 环境）

| 场景 | 内容 |
|---|---|
| KT-S1 | 团队管理员创建 `kg_cypher_query` 实例（绑定托管图）成功，插件出现在模板列表 |
| KT-S2 | 越团队 kgId 创建实例 403 |
| KT-S3 | `schema:true` 回包含实体/关系类型与采样节点、usage 指引 |
| KT-S4 | 合法只读 Cypher（含 `$kgId`）返回正确表格结果 |
| KT-S5 | 缺 `$kgId` 的查询被拒，错误文案含教学指引 |
| KT-S6 | 写语句（CREATE/DELETE/MERGE/CALL 等）逐个被拒 |
| KT-S7 | 行数超 `maxRows` 截断且 `truncated:true` |
| KT-S8 | 超时配置生效（低超时 + 慢查询不可造则跳过，标注 SKIP 原因） |
| KT-S9 | 接入图绑定：schema 摘要来自内省、只读查询走通 |
| KT-S10 | 非团队成员/无实例团队运行 403 |

`local-dev/dynamic-plugin-e2e.mjs`（DYN）补 `kg_cypher_query` 模板注册与实例创建断言。

### 10.3 文档与登记（按仓库 DOC-STANDARD）

- `docs/knowledgegraph/bdd.md` 补 KT 场景、`tdd.md` 补映射并执行、`sdd.md`/`sop.md` 链接；
- AGENTS.md「验证命令」清单登记 `node local-dev/kg-text2cypher-e2e.mjs`；
- `docs/rounds-log.md` 记录轮次闭环证据。

## 11. 新增/改动文件清单（供实现计划展开）

| 位置 | 动作 | 内容 |
|---|---|---|
| `src/aiplugin/MoAI.AIPlugin.Dynamic/Plugins/KgCypherQueryPlugin.cs` | 新增 | 模板类：`[AiPlugin("kg_cypher_query",…)]` + `IDynamicPluginRuntime` 四成员 |
| `src/aiplugin/MoAI.AIPlugin.Dynamic/Models/KgCypherQuery*.cs` | 新增 | Request/Response/Config 模型 |
| `src/aiplugin/MoAI.AIPlugin.Dynamic/CypherReadOnlyGuard.cs` | 新增 | 只读守卫 + `$kgId` 要求 |
| `src/aiplugin/MoAI.AIPlugin.Dynamic/MoAI.AIPlugin.Dynamic.csproj` | 修改 | 引用 `MoAI.KnowledgeGraph.Shared` |
| `src/knowledgegraph/MoAI.KnowledgeGraph.Shared/...` | 新增 | `IKgCypherAccessService` 接口 + DTO |
| `src/knowledgegraph/MoAI.KnowledgeGraph.Core/Services/KgCypherAccessService.cs` | 新增 | kgId→连接解析（托管/接入）、schema 摘要、守卫后执行、表格化 |
| `src/teamplugin/...SaveTeamDynamicPluginCommandHandler.cs` | 修改 | 保存实例时校验 kgId 团队归属（仅 `kg_cypher_query` 模板） |
| `ui/src/...`（团队插件创建页） | 修改 | 绑定图谱下拉 + 名称/描述自动预填 |
| `tests/...` | 新增 | Guard/摘要/截断单测 |
| `local-dev/kg-text2cypher-e2e.mjs` | 新增 | KT-S1~S10 |
| `local-dev/dynamic-plugin-e2e.mjs` | 修改 | DYN 注册/实例断言 |
| 文档四件套 + AGENTS.md + rounds-log | 修改 | 按 §10.3 |

无需数据库迁移（Config 为 JSON 文本）；无需手工注册（`PluginRegistry` 自动扫描）。

## 12. 未来扩展（不在本设计实现）

1. **方案 B**：模板增加 `question` 入参 + 插件内模型调用（工作流普通节点可用）；
2. **写入与审核流**：AI 导入暂存区 + 人工确认入图（业界标配建图体验）；
3. **GraphRAG 检索**：应用绑定 graph_ids、实体向量化 + entity linking + 子图扩展、与 wiki 向量召回融合（KG SDD v2.8 已规划）；
4. **对话记忆图化**（Zep/Graphiti 式）；
5. 答案挂 citation 子图到 G6 画布。
