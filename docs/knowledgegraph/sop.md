# 知识图谱模块操作手册（SOP）

> 关联：[SDD](./sdd.md) ｜ [BDD](./bdd.md) ｜ [TDD](./tdd.md) ｜ [SOP](./sop.md) ｜ 证据：[local-dev/kg-e2e.mjs](../../local-dev/kg-e2e.mjs)、[local-dev/kg-external-e2e.mjs](../../local-dev/kg-external-e2e.mjs)、[local-dev/kg-text2cypher-e2e.mjs](../../local-dev/kg-text2cypher-e2e.mjs)、[local-dev/kg-search-e2e.mjs](../../local-dev/kg-search-e2e.mjs)

## 1. 前置条件

- 知识图谱挂在团队下：先在「团队」页创建团队（见 [../team/sop.md](../team/sop.md)）；建图 / 接入 / 模型 / 节点 / 边维护均需 Owner/Admin（v2 收紧：Member 全只读）。
- 图谱数据存图数据库（默认 Memgraph，Bolt 协议）；schema 与目录存 PostgreSQL。两者都需可用。
- 入口：一级导航「知识图谱」→ `/knowledge-graph` 卡片墙；团队详情「知识图谱」分区也可建图与维护。

## 2. 开启能力与连接图数据库

1. 以 root 登录 → 「系统设置」→「知识图谱（图数据库）」，先填连接信息：`KG_URI`（如 `bolt://127.0.0.1:7687`）、`KG_USERNAME`、`KG_PASSWORD`、`KG_DIALECT`（`memgraph` 默认 / 接外部 Neo4j 选 `neo4j`，影响内省与索引语句）。
2. **开启前实测**：点「测试连接」直接用表单值预检（无需先保存）；保存开关为「开」时也会自动先保存连接项→实测→**通过才落 `KG_ENABLED=true`**，未通过提示原因且不落开关（表单值保留，改完可重试）。API 直调预检：`POST /api/knowledge-graph/test-connection`（仅 root，字段空=回退已保存值）。
3. 设置项无缓存，保存后下一个请求即生效（驱动按连接信息或方言变化自动重建，无需重启）。**开启后不做实时状态检测**：平台不监视图库健康，创建/操作时直连图库，连不上自动报错（驱动建连超时 10s 快速 503）。
4. 未开启或未填地址时：团队图谱页横幅拦截、「新建 / 接入」不可用，强行调用返回 409。

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
- **图览画布（v4 探索工作台，G6）**：图谱详情默认「图览」——顶部工具栏：**搜索定位**（画布内名称匹配高亮并居中，未命中可转关键字重新加载子图）、**布局切换**（力导向/环形/网格，纯前端重排不重新请求）、**子图深度**（1 跳/2 跳，仅托管图）、加载上限（100/200/500）、缩放/适配/全屏/显示设置（边标签显隐）。左下**图例面板**：类型 + 计数多选显隐（画布内过滤）。**双击节点展开一跳邻居、再双击收起**（收起仅回收因该次展开引入且无其它连边的节点）；单击节点选中并开右侧**详情侧栏**（类型/描述/度数/属性 + 关联关系列表可点跳转邻居）；点击边看关系详情；节点大小按度数映射。Admin+ 编辑：**Ctrl+单击多选**——选中恰 2 个可「连关系」（选中序=起点→终点，弹关系类型选择含约束校验）、≥2 个可「**合并**」；右键画布新建实体、右键节点/边删除。召回测试结果可「在画布查看」高亮（`?highlight=` 联动）。
- **实体合并（治理闭环）**：画布 Ctrl+多选 2~20 个疑似重复实体 →「合并」→ 对话框选择**保留目标**（默认度数最高者）→ 差异摘要核对 → 二次确认。语义：源实体删除，其关系重挂到目标（同「起点+关系+终点」已存在则跳过、两端都来自被合并源的自环丢弃），描述拼接、属性深合并（冲突目标优先），目标向量重建。入口二：导入页疑似重复表「去合并」深链预选。约束：合并后关系起止类型不满足时报 400（先调目标节点类型或拆批合并）；影响关系数 ≥20 需确认标记二次确认。**合并不可逆、无软删**——重要图谱合并前建议先在外部留存源数据（JSON 导入的原始文件或图库快照）。
- **画布问图（Text2Cypher，B7）**：画布工具栏搜索框切「AI 问图」模式 → 自然语言提问（如「运价最高的航段是哪条」）→ 后端按图谱结构让对话模型生成只读 Cypher（自动注入 `$kgId` 隔离与只读守卫：写操作/多语句/超长一律拒绝）→ 结果卡展示生成的 Cypher（可复制排查）与返回行数（500 封顶），结果中的实体自动在画布高亮。模型用请求级选择或图谱设置页的「默认抽取模型」。问的是当前图库全量数据（不受画布加载上限影响）。生成质量取决于问题具体度与模型能力；报「未通过只读校验」属守卫拦截，可换问法重试。接入图不支持（走 kg_cypher_query 插件）。
- **画布样式规则（B8）**：图谱设置页「画布样式规则」卡——为每种实体类型选择节点名后追加显示的内容（某属性值 / 类型名 / 不显示），管理员配置后全图生效（Member 跟随）。典型用法：港口类型显示「距离公里」属性、人员类型显示类型名，让画布一眼可读。规则存图谱（画布实时应用，刷新即生效）；全部选「不显示」保存即清除回默认。
- **AI 导入文件生成图谱**：图览工具栏「AI 导入」→ 上传文档（docx/pdf/txt/md 等，≤20MB）→ 选团队可用对话模型 → 开始导入。后端用 Maomi.ToMarkdown 提取文本（超 1.2 万字符截断，仅前部分导入），AI 按图谱**现有模型**抽取实体与关系写入图库；无模型的图谱会提示先在「模型」页定义。导入质量取决于模型能力与文本与模型领域的匹配度，导入后可在图览/实例页检查并清理。30 秒级同步执行，大文件建议拆分后分批导入。
- **接入外部图谱**：新建 →「接入已有」→ 填名称与目标 database（外部实例的库名；Memgraph 社区版单库，填默认库）。接入后只读内省标签 / 关系类型 / 属性键；外部服务直写图库的数据会随内省自动识别（schema 有 5 分钟 Redis 缓存，模型页可点「刷新内省」强制重新识别并对比上次基线显示新增 / 消失项）。
- **接入图图览**：接入图详情也有「图览」——按标签色板过滤 + 关键字搜索 + 点节点一跳展开；外部节点无平台 id 体系，以 `elementId` 定位、`name/title/id` 属性启发式取名（要求 Memgraph ≥ 2.14）。
- **设置图谱头像**：图谱详情「设置」页 → 点头像或「更换头像」上传（支持 JPG/PNG，≤5MB，走存储直传管线）；仅 Owner/Admin；列表卡片同步展示。
- **配置向量化与图检索（SP-A）**：托管图详情「设置」页选 embedding 模型（其下方「**默认抽取模型**」可配 AI 导入缺省对话模型：导入未显式选模型时回退此默认，请求级选择优先；留空则每次导入必须选模型）（团队可用向量化模型，`GET /model-options` 的 `embeddingModels` 桶）+ 维度（默认 1024）；配置保存后已有节点自动全量重嵌（上限 5000，超出部分告警不重嵌），之后的节点增删改经 MQ 增量同步，延迟秒级。配置好后可在图检索中语义搜实体（应用对话 `search_knowledge_graph` 工具、流程 `kgSearch` 节点、检索 API `POST /{id}/search`）；未配模型的图不参与检索（检索 API 直接 409 提示）。
- **导入菜单页（v3.2；AI 导入 v4.1 异步任务化）**：托管图详情「导入」菜单，两个页签——**AI 智能导入**（上传 42 种支持格式的文档 ≤20MB → **登记异步导入任务**（不再同步等待）：Hangfire 依次 提取文本 → 对话模型按图谱现有模型抽取实体/关系 → **幂等合并落库**；页签下方「导入任务」历史表实时轮询状态（排队中/提取文本/AI 抽取/合并落库/完成/失败/已取消）与进度，失败任务显示原因并可「重试」，排队/执行中可「取消」，完成时提示统计并自动刷新画布）与 **JSON 导入**（粘贴或上传结构化 JSON，可「下载示例 JSON」照改；先「预检」看预测结果不落库，再导入；类型按名称引用可自动创建、节点带业务 key 幂等 upsert、坏行逐条报告不阻断；开启「疑似重复检测」时对新建节点按向量相似度报告与已有节点/本批次高度相似的实体对，导入前核对可避免重复数据；疑似重复行可直接点「**去合并**」跳画布预选两个实体进入合并流程）。**重复导入同一文档是安全的**（D26 证据链）：命中已有实体的描述去重拼接、缺失属性补齐、来源证据记账（上限 20 条），同三元组关系跳过——图库不重复增长。批量结构化数据优先走 JSON 导入（也等价于外部接口 `POST /api/teamres/knowledge-graph/{kgId}/import`）。
- **召回测试（v3.4）**：托管图详情「召回测试」菜单——输入自然语言问题做向量召回（返回实体 + 相似度得分 + 一跳关系），可调召回条数、相似度阈值与**邻居数**（每命中实体按度数排序保留的邻居数 0-30 默认 10——连接越多的邻居优先进入上下文）；开启「AI 优化问题」让模型先把问题改写成适合检索的查询，「AI 回答」基于召回事实直接回答。结果区可「**在画布查看**」：跳转图览画布高亮命中实体（画布上缺失的节点自动经邻接补拉后聚焦）。前置：图谱设置页已配置向量化模型。用于调参验证「应用对话 / 工作流 / MCP 里语义检索的效果」。
- **删除图谱**：托管图 = 软删登记 + 清空该图在图库的节点与边 + 清空 `__kg_{id}` 向量集合；接入图 = **仅移除平台登记，绝不动外部数据**。

## 4. 常见问题

| 现象 | 处理 |
|---|---|
| 新建 / 接入返回 409「未开启知识图谱能力」 | 系统设置开启 `KG_ENABLED` 并填写 `KG_URI`；仅开启但地址为空同样 409 |
| 建图 / 图操作返回 503「无法连接图数据库」 | Memgraph/Neo4j 不可达或凭据错误；核对 `KG_URI / USERNAME / PASSWORD` 与容器状态（驱动建连超时 10 秒，快速报错不长时间挂起） |
| 图览画布提示 503 | 图数据库不可达时画布查询直连图库，连接失败自动报错（**列表接口不触碰图数据库**——图库宕机不影响图谱列表/新建入口，实时计数展示已退役）；按上一行恢复图库即可 |
| 设置页「测试连接」未通过 / 开启开关失败 | 保存开关为开时自动实测图库连通性，未通过不落开关：核对地址可达（`bolt://`）、凭据、方言（Memgraph/Neo4j）；错误地址 8 秒内返回原因 |
| AI 导入任务 failed | 任务详情/历史表「说明」列显示原因：文件不存在或提取失败（objectKey 失效/格式不支持）、模型不存在或未启用（渠道停用/授权变化）、图库不可达（按 503 行排查）；修复后点「重试」幂等重跑不产生重复数据 |
| 画布区域空白、无图例无工具栏按钮 | G6 引擎随前端打包（仅路由级分包，无外部 CDN），加载失败多为浏览器过旧不支持 Canvas 2D——控制台查看报错；不支持时画布降级为空容器，导入/召回/模型等其余功能不受影响 |
| 合并返回 400「起止类型约束不满足」 | 被合并实体的关系重挂到目标后，目标节点类型不满足该关系类型的起/止类型定义；先在「模型」页调整目标实体类型，或把不匹配的源实体拆出本次合并 |
| 合并返回 409「影响 N 条关系」 | 影响面达到阈值（≥20 条关系）的二段确认：前端会弹不可逆确认，直连 API 时需携带 `confirm=true` 重试 |
| 图查询 503 文案带 `（DatabaseException: ...）` | 503 已携带底层异常类型与消息（2026-11-09 起）：DatabaseException 多为 Memgraph 方言差异或查询语法问题（历史案例：`size((n)--())` pattern 在 Memgraph 3.13 不支持，已改 `size([(n)--() \| 1])`）；ServiceUnavailableException/SessionExpiredException 多为图库宕机或连接池死连接（重启后端可清池） |
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
| MCP 端点返回 401 | 凭证缺失或伪造：请求须携带应用接入 key（`Authorization: Bearer moai-ac-…` 或 `x-api-key` 头） |
| MCP 端点返回 403 `insufficient_scope` | moai-tr- 资源 key 越出包围圈（该 key 只能访问绑定资源端点）；moai-ac- 接入 key 全量放行无范围概念（scope 体系已退役）。改用绑定资源的 key 或接入 key |
| MCP 端点返回 404 | 地址中的 wikiId/kgId 不存在或属于其他团队（不泄露存在性）；从对应详情「MCP」菜单复制完整地址 |
| MCP 工具列表里出现别域工具 / 调用提示「在当前 MCP 端点不可用」 | 工具按接入地址分域：wiki 地址只有知识库三工具、图谱地址只有图谱四工具；核对地址资源 |
| MCP 向量召回结果为空 | 图谱未配置向量化模型（工具返回 skippedHint 提示）或阈值过高；先配置向量化并降低 minScore |

## 5. 验收流程

1. `dotnet build src/MoAI/MoAI.csproj` → 0 错误；`dotnet test tests/MoAI.KnowledgeGraph.Tests/`。
2. 后端运行且 Memgraph 可达、`KG_ENABLED=true` 后执行 `node local-dev/kg-e2e.mjs http://127.0.0.1:5000` → 覆盖 [@KG-S1](../knowledgegraph/bdd.md#kg-s1)…[@KG-S21](../knowledgegraph/bdd.md#kg-s21) 与 v4 探索工作台段 [@KG-S37](../knowledgegraph/bdd.md#kg-s37)~[@KG-S40](../knowledgegraph/bdd.md#kg-s40)（depth/degree/实体合并，独立图自包含；S15 成员只读由单测覆盖）+ 测试连接预检 [@KG-S46](../knowledgegraph/bdd.md#kg-s46) + 列表去图库化 [@KG-S47](../knowledgegraph/bdd.md#kg-s47)；无图数据库时脚本 SKIP（S46 的 401/403/400/ok=false 各路可无图库直测，见 tdd 自检记录 2026-11-08 真机实测）。
3. 外部开放接口验收：`node local-dev/kg-external-e2e.mjs http://127.0.0.1:5210` → 覆盖 KX-01~KX-08（应用 token 团队级授权、类型/节点/边 CRUD 与批量导入、connected 只读；前置：至少一个应用接入 key，脚本自建）；无图数据库或能力未开启时 SKIP。
4. 图检索验收（SP-A）：`node local-dev/kg-search-e2e.mjs http://127.0.0.1:5000` → 覆盖 [@KGS-S1](../knowledgegraph/bdd.md#kgs-s1)~[@KGS-S9](../knowledgegraph/bdd.md#kgs-s9)（前置：RabbitMQ + pgvector + 图数据库，脚本自建本地 embeddings 桩渠道，无需真实模型；向量化为 MQ 异步，脚本自带轮询）。
5. MCP 验收：`node local-dev/kg-mcp-e2e.mjs http://127.0.0.1:5000` → 覆盖 [@KGM-S1](../knowledgegraph/bdd.md#kgm-s1)~[@KGM-S8](../knowledgegraph/bdd.md#kgm-s8)（鉴权与资源 key 包围圈门禁、工具域隔离、四只读工具、协议行为；本地桩 embedding）；知识库侧 `node local-dev/wiki-mcp-e2e.mjs`；凭证总账户 `node local-dev/teamres-key-e2e.mjs`。
6. 浏览器走查：`/knowledge-graph` 与团队「知识图谱」分区建托管图（含模板）→ 图览（工具栏六件套 / 图例多选显隐 / 双击展开收起 / 详情侧栏关联关系跳转 / 搜索定位 / 布局切换不重新请求 / 截断提示）→ 维护页步骤条切换 模型 / 实例 / 关系 维护（Member 只读）→ 导入页 JSON 导入 + 疑似重复「去合并」深链 → 召回测试「在画布查看」高亮 → 多选连关系 / 合并对话框 → 删除清库；接入外部库 → 只读内省与刷新 → 接入图图览（标签过滤 / 展开）→ 删除仅移除登记；重启 Memgraph 容器验证快照恢复。

### 验收记录

| 日期 | 结果 |
|---|---|
| 2026-09-10 | 后端构建 0 错误 + 单测 33/33 通过；E2E 未执行（环境无 Neo4j，待补） |
| 2026-09-14 | v2 构建 0 错误 + 单测 39/39；前端 typecheck/lint 0 错误、vitest 265/265；**E2E 40/40 PASS**（真实 Memgraph 3.13.0，覆盖 S1~S16；期间修复 Memgraph 3.x 内省方言与路由回填字段校验 400 两处缺陷，见 tdd 自检记录） |
| 2026-09-15 | v2.1 动态内省 + 接入图画布：单测 **40/40**、**E2E 47/47 PASS**（新增 S17~S19）、前端 typecheck/lint 0 错误、vitest 258/258 |
| 2026-09-15 | v2.2 图谱头像：单测 **42/42**、**E2E 52/52 PASS**（新增 S20，真实存储直传验证）、前端 typecheck/lint 0 错误、vitest 258/258 |
| 2026-09-21 | v2.5 物流模板 + 默认图览：单测 **43/43**、**E2E 61/61 PASS**（5310 独立实例）、前端 typecheck/lint 0 错误、vitest 424/424 |
| 2026-09-27 | v3 图览瘦身 + 维护页改版：前端 typecheck/lint 0 错误、vitest **441/441**（新增旧路径重定向、维护步骤条用例）；浏览器实测（Edge，托管图 kg104）：图览无步骤条/导入按钮、画布撑满视口、缩放/适应/全屏可用（fullscreenElement 生效、图标切换）、维护步骤条三步切换、旧 `/relations` 重定向第 3 步；后端无改动 |
| 2026-09-27 | v3.1 图览画布引擎切换 React Flow：`@antv/g6` 卸载、新增 `@xyflow/react` ^12 + `d3-force`；前端 typecheck/lint 0 错误、vitest **441/441**（jsdom 直接渲染 React Flow，无需 mock）；浏览器实测（Edge，kg104）：11 节点/15 边渲染、卡片节点与带标签边、点节点开抽屉、类型过滤命中 4 港口、全屏 true、右键新建实体弹窗、22 连接桩、视口适配全图完整显示 |
| 2026-09-22 | SP-A 图检索消费层：KG 单测 **81/81**、App **39/39**、Workflow **77/77**、AI.Core **73/73**；**KGS E2E 40/40 PASS**（kg-search-e2e，本地 embeddings 桩，真实后端 + Memgraph + RabbitMQ + pgvector）、KT E2E 15/15 PASS |
| 2026-11-08 | v4 画布探索工作台（G6 重写 + 实体合并 + 召回联动）：dotnet build 0 error、KG 单测 **85/85**（新增 merge 4 例）、vitest 全仓 **795/795**、typecheck/lint 0 error；浏览器走查（Edge headless @ playwright-cli，kg237）G6 运行时/工具栏/图例/布局切换/设置弹层全通过，走查修复布局切换重请求缺陷；**kg-e2e v4 扩展段（S37/S38/S40，82 断言）待 Memgraph 恢复执行**（192.168.50.199:7687 宕机中） |
| 2026-11-09 | v4.2~v4.4（召回升级/抽取模型/问图/样式规则）+ P1 扫尾：dotnet build 0 error、KG 单测 **103/103**、全量 vitest **811/811**、typecheck/lint 0 error；**B8 后端链路真实走查通过**（Edge headless：设置页两卡渲染 → 真实类型 id 324 PUT `__type__` → 200 → 详情回显 `{"324":"__type__"}` → null 清除 200；错误路径实测「不存在的实体类型」400 带文案）；kg-search-e2e 预置 KGS-S44 断言（50）；**全量 e2e 与画布数据走查仍待 Memgraph**（7687 仍拒连） |
| 2026-11-09（二）**Memgraph 恢复 + 三套 e2e 全量收口**：SSH 拉起 192.168.50.199 的 memgraph 容器（Exited 255，宿主机重启导致）；**期间揪出并修复一个 P0 级兼容缺陷**——B3 轮新加的度数 Cypher `size((m)--())`（pattern 作 size 参数，openCypher 标准）**Memgraph 3.13 不支持**（报 `'size' argument at position 1 must be either 'numeric...'`，且空结果集下惰性求值不报错——首轮直连验证是假阳性），导致 /search、recall-test、canvas、neighbors 在任何有数据的 Memgraph 上全 503；修复为 `size([(m)--() \| 1])` 模式推导写法（真实数据验证 degree=5/3/1 求值正确），4 处 Cypher 全部替换；顺带把图库 503 文案带上底层异常类型与消息（`（DatabaseException: ...）`，排障可直接定位，6 处）；另修复 neighbors/depth2 扩展查询 RETURN 缺 propsJson/srcJson 致 MapNode KeyNotFound 500、kg-search-e2e 对话模型渠道缺团队授权（脚本缺口补 PUT authorization）、v4 段建图名含连字符违反 D55 命名规则、S37a/S40b 场景设计过时（自环化边不进约束校验/新边改变度数预期）。**最终结果——kg-search-e2e 49/49（含 KGS-S44a…c）、kg-import-e2e 32/32（含 KG-S42b…c 幂等重导与任务治理）、kg-e2e 84/84（含 KG-S37…S40 v4 段）三套全绿**；KG-S42b2 实证「重导 done 且 新建 0/合并 2/新边 0」即 B1 证据链幂等合并真机验证通过。文档：AGENTS.md 三行计数与 PASS 日期、sdd §9 已知问题（Memgraph size() 方言差异）、本表与排障表。B1 幂等合并单侧还差「合并后 srcJson 证据可读」断言（低优先） |
| 2026-11-09（三）**真实数据画布视觉走查 + 遗留清理**：Memgraph 恢复后首轮真实数据走查（Edge headless，kg237 云途业务图谱 9 节点 8 边）——G6 真实渲染（类型色板着色/度数映射节点大小/图例计数 1·5·3/关系边标签与方向箭头/力导向布局）、**样式规则 caption 视觉实证**（配「产品→类型名」后节点 label 显示「云途网盘 · 产品」截图实证；期间揪出 Kiota 把 Dictionary 收进 additionalData 的映射坑——detail wrapper 归一化修复，同节点 properties 坑）；适配视图/搜索定位/放大均正常。**遗留清理**：round345 遗留「修复验证临时图谱」（id=286）删除 200（图库恢复后删除链路首次实跑通过：软删+清图库+清向量）；样式测试规则清除恢复默认。至此 v4 重构全部验收项闭环，无遗留代码工作。 |
| 2026-11-08 | v4.3 图数据库维护去实时化（列表不触图库 + 开启前测试连接）：KG.Api 编译 0 错误（宿主全量仅并行线 bin 文件锁，无 CS 错误）、typecheck/lint 0 error、vitest Settings **30/30** + TeamKnowledgeGraphs **13/13**、syncapi 重生成；**图库宕机真机实测**（192.168.50.199 不可达）：test-connection 存储值/错误地址 2~3s `ok=false` 携带原因、非法地址/方言 400、未认证 401、普通用户 403、**列表 44ms 返回**（改造前同场景 3s+ 且每请求泄漏 N 个驱动重试任务）；kg-e2e 扩 S46a~f/S47a（**90 断言**）待 Memgraph 恢复执行 |

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

## 8. 外部 MCP 客户端接入（知识图谱 / 知识库）

把平台的团队知识以 **MCP（Model Context Protocol）** 标准暴露给外部 AI 客户端（Claude Desktop、Cursor 等）。streamable HTTP 无状态传输：无需换 token，key 直接进请求头，每个请求独立鉴权。

**前置**：凭证二选一——①团队「应用接入」创建接入 key（`moai-ac-…`，全量访问，无范围勾选）；②在图谱/知识库详情「MCP」菜单的资源 key 卡片生成该资源的 `moai-tr-` key（仅能访问本资源端点）。地址与工具从对应详情菜单复制——图谱详情「MCP」菜单、知识库详情「MCP」菜单均有完整地址、资源 key 卡片与复制按钮。

| 域 | 地址 | 工具（均只读） |
|---|---|---|
| 知识图谱 | `{服务地址}/api/teamres/knowledge-graph/{kgId}/mcp` | `list_knowledge_graphs` / `get_knowledge_graph_schema` / `search_knowledge_graph_nodes`（关键字搜实体）/ `search_knowledge_graph_recall`（语义召回 + 一跳关系） |
| 知识库 | `{服务地址}/api/teamres/wiki/{wikiId}/mcp` | `list_knowledge_bases` / `search_knowledge_base_files` / `search_knowledge_base_recall`（切片召回） |

**Claude Desktop / Cursor 配置**（`mcpServers` 片段，headers 携带 key）：

```json
{
  "mcpServers": {
    "moai-knowledge-graph": {
      "url": "http://<服务地址>/api/teamres/knowledge-graph/<kgId>/mcp",
      "headers": { "Authorization": "Bearer moai-ac-xxxxxxxxxxxxxxxx" }
    }
  }
}
```

> 客户端对远程 MCP 的支持形态有差异：支持 `url` 直连（streamable HTTP）的客户端直接可用；仅支持 stdio 的客户端需经 `mcp-remote` 之类的桥接器（`npx mcp-remote <url> --header "Authorization: Bearer <key>"`）。

**curl 调试**（无状态模式，免 initialize 直接列工具）：

```bash
curl -s -X POST 'http://<服务地址>/api/teamres/knowledge-graph/<kgId>/mcp'   -H 'Authorization: Bearer moai-ac-xxxx'   -H 'Content-Type: application/json' -H 'Accept: application/json, text/event-stream'   -d '{"jsonrpc":"2.0","id":1,"method":"tools/list"}'

# 召回示例
curl -s -X POST 'http://<服务地址>/api/teamres/knowledge-graph/<kgId>/mcp'   -H 'Authorization: Bearer moai-ac-xxxx'   -H 'Content-Type: application/json' -H 'Accept: application/json, text/event-stream'   -d '{"jsonrpc":"2.0","id":2,"method":"tools/call","params":{"name":"search_knowledge_graph_recall","arguments":{"queryText":"上海到宁波的运价","top":5}}}'
```

**排错**：见 §4 常见问题 MCP 相关行（401 凭证 / 403 未勾选范围 / 404 资源不存在或跨团队 / 工具域不匹配）。另注意：地址里的 kgId/wikiId 决定数据范围（单资源授权），换资源需换地址；接入图（外部图库登记）不参与 MCP 召回，结构化查询可用 Text2Cypher 插件（§6）。
