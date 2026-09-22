# 知识图谱模块操作手册（SOP）

> 关联：[SDD](./sdd.md) ｜ [BDD](./bdd.md) ｜ [TDD](./tdd.md) ｜ [SOP](./sop.md) ｜ 证据：[local-dev/kg-e2e.mjs](../../local-dev/kg-e2e.mjs)、[local-dev/kg-external-e2e.mjs](../../local-dev/kg-external-e2e.mjs)、[local-dev/kg-text2cypher-e2e.mjs](../../local-dev/kg-text2cypher-e2e.mjs)、[local-dev/kg-search-e2e.mjs](../../local-dev/kg-search-e2e.mjs)

## 1. 前置条件

- 知识图谱挂在团队下：先在「团队」页创建团队（见 [../team/sop.md](../team/sop.md)）；建图 / 接入 / 模型 / 节点 / 边维护均需 Owner/Admin（v2 收紧：Member 全只读）。
- 图谱数据存图数据库（默认 Memgraph，Bolt 协议）；schema 与目录存 PostgreSQL。两者都需可用。
- 入口：一级导航「知识图谱」→ `/knowledge-graph` 卡片墙；团队详情「知识图谱」分区也可建图与维护。

## 2. 开启能力与连接图数据库

1. 以 root 登录 → 「系统设置」→「知识图谱（图数据库）」，把 `KG_ENABLED` 置为 `true`。
2. 配置：`KG_URI`（如 `bolt://127.0.0.1:7687`）、`KG_USERNAME`、`KG_PASSWORD`、`KG_DIALECT`（`memgraph` 默认 / 接外部 Neo4j 选 `neo4j`，影响内省与索引语句）。
3. 设置项无缓存，保存后下一个请求即生效（驱动按连接信息或方言变化自动重建，无需重启）。
4. 未开启或未填地址时：「新建 / 接入」不可用，强行调用返回 409。

### 本地 Memgraph

`docker-compose.yml` 已内置可选 `memgraph` 服务（memgraph-platform：Bolt 7687、MgLab 3000）：

```bash
docker compose up -d memgraph     # 已配 snapshot 持久化（300s 间隔 + 退出快照），数据卷 memgraph_data
```

- 社区版**默认纯内存**，compose 已带 `--storage-snapshot-interval-sec=300 --storage-snapshot-on-exit=true`；切勿移除，否则容器重启丢图。
- MgLab 控制台 <http://127.0.0.1:3000> 可直查 Cypher；系统设置里 `KG_URI` 填 `bolt://127.0.0.1:7687`。

## 3. 日常操作

- **建托管图谱**：团队详情「知识图谱」分区 → 新建 →「平台托管」→ 填名称（≤50，**全平台唯一**，跨团队不得重名）、简介、可选模板（空白 / 物流运输 / 组织人脉 / 事件脉络），还可**同步上传头像**（仅图片 ≤5MB；登记失败不影响创建，可稍后在设置页补传）。物流运输模板建图即成一张示例航线网络：港口 上海/宁波/厦门/深圳、5 条航段（自带 运输方式/距离公里/运输价格/时效天 属性）、承运商 中远海运/马士基，含 15 条 出发/抵达/承运 关系——图览直接可看，且可演示「上海到深圳」直达最短（1500 公里）但中转更便宜（经宁波 4700 元 < 直达 5200 元）；示例数据可改可删。托管图谱零库名：所有托管图共用一个图库 keyspace，经 `kgId` 属性隔离。
- **维护模型**：图谱详情「模型」页增删改实体类型、关系类型；关系类型可选起止实体类型约束；实体类型可配置**属性设置**（属性名 / 类型：文本·数字·布尔·日期 / 是否必填 / 说明，≤50 个）。
- **维护节点 / 边**：仅 Admin+ 可增删改（v1 曾放开 Member，v2 收紧）；节点删除连带其边；边新增 / 修改校验起止约束；实例表单按所属类型的属性定义渲染，必填属性强制填写。
- **维护节点 / 边**：仅 Admin+ 可增删改（v1 曾放开 Member，v2 收紧）；节点删除连带其边；边新增 / 修改校验起止约束。
- **图览画布**：图谱详情默认「图览」——有界子图（上限 500 节点）、实体类型色板过滤、关键字搜索、**拖动节点调整位置**、**点击节点展开一跳邻接并弹出详情抽屉**（类型/描述/属性，航段可直接看距离与运价）、**点击边查看关系类型与起止**、边上显示关系类型名；编辑态点起点节点再点终点节点建立关系、右键空白建实例、右键节点/边删除；拖空白平移、滚轮缩放。录入仍在实体 / 关系页。
- **AI 导入文件生成图谱**：图览工具栏「AI 导入」→ 上传文档（docx/pdf/txt/md 等，≤20MB）→ 选团队可用对话模型 → 开始导入。后端用 Maomi.ToMarkdown 提取文本（超 1.2 万字符截断，仅前部分导入），AI 按图谱**现有模型**抽取实体与关系写入图库；无模型的图谱会提示先在「模型」页定义。导入质量取决于模型能力与文本与模型领域的匹配度，导入后可在图览/实例页检查并清理。30 秒级同步执行，大文件建议拆分后分批导入。
- **接入外部图谱**：新建 →「接入已有」→ 填名称与目标 database（外部实例的库名；Memgraph 社区版单库，填默认库）。接入后只读内省标签 / 关系类型 / 属性键；外部服务直写图库的数据会随内省自动识别（schema 有 5 分钟 Redis 缓存，模型页可点「刷新内省」强制重新识别并对比上次基线显示新增 / 消失项）。
- **接入图图览**：接入图详情也有「图览」——按标签色板过滤 + 关键字搜索 + 点节点一跳展开；外部节点无平台 id 体系，以 `elementId` 定位、`name/title/id` 属性启发式取名（要求 Memgraph ≥ 2.14）。
- **设置图谱头像**：图谱详情「设置」页 → 点头像或「更换头像」上传（支持 JPG/PNG，≤5MB，走存储直传管线）；仅 Owner/Admin；列表卡片同步展示。
- **配置向量化与图检索（SP-A）**：托管图详情「设置」页选 embedding 模型（团队可用向量化模型，`GET /model-options` 的 `embeddingModels` 桶）+ 维度（默认 1024）；配置保存后已有节点自动全量重嵌（上限 5000，超出部分告警不重嵌），之后的节点增删改经 MQ 增量同步，延迟秒级。配置好后可在图检索中语义搜实体（应用对话 `search_knowledge_graph` 工具、流程 `kgSearch` 节点、检索 API `POST /{id}/search`）；未配模型的图不参与检索（检索 API 直接 409 提示）。
- **删除图谱**：托管图 = 软删登记 + 清空该图在图库的节点与边 + 清空 `__kg_{id}` 向量集合；接入图 = **仅移除平台登记，绝不动外部数据**。

## 4. 常见问题

| 现象 | 处理 |
|---|---|
| 新建 / 接入返回 409「未开启知识图谱能力」 | 系统设置开启 `KG_ENABLED` 并填写 `KG_URI`；仅开启但地址为空同样 409 |
| 建图 / 图操作返回 503「无法连接图数据库」 | Memgraph/Neo4j 不可达或凭据错误；核对 `KG_URI / USERNAME / PASSWORD` 与容器状态 |
| 返回 409「图数据库连接地址无效」 | `KG_URI` 不是合法 URI（须含 scheme，如 `bolt://`） |
| 接入返回 400「数据库不存在或无法访问」 | database 名写错或无权限；先用 MgLab / Neo4j Browser 确认 |
| 接入图新增节点 / 类型返回 409 | 设计如此：接入图只读；图览/邻接查询 v2.1 起已支持接入图 |
| 接入图 schema 看不到刚写入的标签 | 内省结果有 5 分钟缓存；模型页点「刷新内省」立即重新识别 |
| 设置头像返回 404「头像文件不存在或未完成上传」 | objectKey 未登记或直传未完成；重新经设置页上传（存量库需先执行 `asserts/knowledge_graph.sql` 补 `avatar_path` 列） |
| 编辑实体 / 边 / 类型返回 400「id 不正确」 | v1 遗留缺陷（validator 校验路由字段），v2.3 已修复；若复现请更新后端 |
| 删除实体类型 / 关系类型返回 409 | 该类型下仍有数据或被引用；先清理 |
| 同名建图返回 409 | v2 起名称**全平台唯一**；改名或删除旧图后重建 |
| 容器重启后图数据丢失 | compose 的快照持久化参数被移除；恢复 `MEMGRAPH` 环境变量与数据卷 |
| 成员看不到新增 / 编辑 / 删除按钮 | v2 权限收紧：Member 全只读，写操作需 Admin+ |
| Memgraph 3.x 报 `no procedure named 'mg.labels'` 或 `UNWIND must be a list` | v2.1 已修复：memgraph 方言内省改为数据派生查询（3.x 移除了 `mg.*` 过程，且 `labels(n)` 单标签返回字符串）；升级前版本请拉取最新代码 |

## 5. 验收流程

1. `dotnet build src/MoAI/MoAI.csproj` → 0 错误；`dotnet test tests/MoAI.KnowledgeGraph.Tests/`。
2. 后端运行且 Memgraph 可达、`KG_ENABLED=true` 后执行 `node local-dev/kg-e2e.mjs http://127.0.0.1:5000` → 覆盖 [@KG-S1](../knowledgegraph/bdd.md#kg-s1)…[@KG-S19](../knowledgegraph/bdd.md#kg-s19)（S15 成员只读由单测覆盖）；无图数据库时脚本 SKIP。
3. 外部开放接口验收：`node local-dev/kg-external-e2e.mjs http://127.0.0.1:5210` → 覆盖 KX-01~KX-08（应用 token 团队级授权、类型/节点/边 CRUD 与批量导入、connected 只读；前置：至少一个团队接入 key，脚本自建）；无图数据库或能力未开启时 SKIP。
4. 图检索验收（SP-A）：`node local-dev/kg-search-e2e.mjs http://127.0.0.1:5000` → 覆盖 [@KGS-S1](../knowledgegraph/bdd.md#kgs-s1)~[@KGS-S9](../knowledgegraph/bdd.md#kgs-s9)（前置：RabbitMQ + pgvector + 图数据库，脚本自建本地 embeddings 桩渠道，无需真实模型；向量化为 MQ 异步，脚本自带轮询）。
5. 浏览器走查：`/knowledge-graph` 与团队「知识图谱」分区建托管图（含模板）→ 图览过滤 / 搜索 / 展开 → 模型 / 节点 / 边维护（Member 只读）→ 删除清库；接入外部库 → 只读内省与刷新 → 接入图图览（标签过滤 / 展开）→ 删除仅移除登记；重启 Memgraph 容器验证快照恢复。

### 验收记录

| 日期 | 结果 |
|---|---|
| 2026-09-10 | 后端构建 0 错误 + 单测 33/33 通过；E2E 未执行（环境无 Neo4j，待补） |
| 2026-09-14 | v2 构建 0 错误 + 单测 39/39；前端 typecheck/lint 0 错误、vitest 265/265；**E2E 40/40 PASS**（真实 Memgraph 3.13.0，覆盖 S1~S16；期间修复 Memgraph 3.x 内省方言与路由回填字段校验 400 两处缺陷，见 tdd 自检记录） |
| 2026-09-15 | v2.1 动态内省 + 接入图画布：单测 **40/40**、**E2E 47/47 PASS**（新增 S17~S19）、前端 typecheck/lint 0 错误、vitest 258/258 |
| 2026-09-15 | v2.2 图谱头像：单测 **42/42**、**E2E 52/52 PASS**（新增 S20，真实存储直传验证）、前端 typecheck/lint 0 错误、vitest 258/258 |
| 2026-09-21 | v2.5 物流模板 + 默认图览：单测 **43/43**、**E2E 61/61 PASS**（5310 独立实例）、前端 typecheck/lint 0 错误、vitest 424/424 |
| 2026-09-22 | SP-A 图检索消费层：KG 单测 **81/81**、App **39/39**、Workflow **77/77**、AI.Core **73/73**；**KGS E2E 40/40 PASS**（kg-search-e2e，本地 embeddings 桩，真实后端 + Memgraph + RabbitMQ + pgvector）、KT E2E 15/15 PASS |

## 6. kg_cypher_query 插件运维（Text2Cypher）

- **是什么**：平台内置动态插件模板 `kg_cypher_query`——把一张团队图谱（托管或接入）暴露为 Agent 工具，对话模型以只读 Cypher 即席查图：先 `{"schema":true}` 自描述拿图结构与用法，再写 MATCH 查询。设计见 [Text2Cypher 设计文档](../superpowers/specs/2026-09-21-kg-text2cypher-plugin-design.md)，场景 [@KT-S1~S10](./bdd.md#feature-text2cypher-查图插件消费kt-s)。
- **实例创建入口**：团队详情「插件」→ 动态插件 → 新建，模板选 `kg_cypher_query`。「绑定图谱」下拉列本团队托管图 + 接入图，选中后前端自动预填工具名称与描述（从图谱 schema 拉实体/关系类型拼说明，保留手改自由度）。配置仅存 `kgId / timeoutSeconds(1-300，默认 30) / maxRows(1-1000，默认 200)`，不存连接串；保存时校验图谱存在（404）且属于本团队（越团队 403）。
- **只读三层保障**：
  1. **文本守卫** `CypherReadOnlyGuard`：对注释与字符串字面量**先剥除后扫描**，黑名单 CREATE/MERGE/DELETE/DETACH/SET/REMOVE/LOAD CSV/FOREACH/CALL/DROP + 只读首关键字白名单 + 单语句 + 8000 字符上限，命中即 400 教学式报错（校验先于连接，不依赖图库可达）；
  2. **托管图隔离**：`$kgId` 由系统自动注入并强制出现在查询中（缺失报教学错误），结果侧再对返回图元素逐个核对 kgId 归属（防字面量绕过），跨图数据拒绝返回；接入图整库即图、免 `$kgId`；
  3. **资源限制**：行数超 `maxRows` 截断（`truncated:true`），单次查询按 `timeoutSeconds` 超时。
- **常见报错对照**：

| 现象 | 说明与处理 |
|---|---|
| 报「托管图谱查询必须包含 {kgId: $kgId} 过滤…」 | 教学文案：模型漏写 `$kgId` 过滤，错误文本会回喂对话循环自行修正，无需人工干预；接入图无此要求 |
| 报错含「只读」 | 写语句（CREATE/MERGE/DELETE/SET/CALL 等）被文本守卫拒绝，属预期防护 |
| 创建实例返回 403「只能绑定本团队的知识图谱」 | 绑定的图谱属于其他团队；只能绑定本团队托管图/接入图 |
| 创建实例返回 400 | 配置非法：kgId 非正整数、timeoutSeconds/maxRows 越界（1-300/1-1000）等，按提示修正 |

- **验证**：`node local-dev/kg-text2cypher-e2e.mjs`（依赖 Memgraph 与含 `kg_cypher_query` 的新构建后端，图库不可达时托管图场景以错误退出）；单测 `dotnet test tests/MoAI.AIPlugin.Dynamic.Tests/` → 36/36。

## 7. 图检索运维（SP-A）

- **向量不同步排查**（节点增删改后检索结果未更新）：
  1. **MQ 死信**：RabbitMQ 控制台查 `kg.node.embedding` 队列积压——消费失败自动重投，**重试耗尽后 Ack 放弃**（仅后端日志 `kg embedding delta dropped after retries`，不阻塞队列）；放弃的节点在下次被编辑、或配置变更全量重嵌时自动补齐。
  2. **未配模型静默跳过**：图谱未配置向量化模型时，增量 delta **不报错、直接跳过**（设计语义，非故障）——先到图谱设置页确认已选 embedding 模型；配置保存即触发全量重嵌补齐存量节点。
  3. **5000 全量重嵌上限**：配置 embedding 模型时全量重嵌最多取 5000 个节点，超出部分仅记 WARNING（`达到全量重嵌上限 5000`）；超限图谱需分批触发（编辑节点或重改配置）。
  4. **删图孤儿集合兜底**：删除托管图**先清 `__kg_{id}` 向量集合、后软删登记**（顺序不可换：软删后全局 IsDeleted 过滤器会让清理静默失效）；清理失败仅记日志、删除继续——孤儿集合只占存储，不影响业务（图谱 id 不会复用）。
- **配置变更行为**：换模型或维度 → 旧向量集合与新维度不兼容，先**整集合删除**再全量重嵌（清理与重嵌均 best effort，失败仅日志、不影响已保存配置）；重复提交相同配置不清理集合、仅重发幂等 delta（无害）。
