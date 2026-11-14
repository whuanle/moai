# 知识图谱模块验证映射（TDD）

> 关联：[SDD](./sdd.md) ｜ [BDD](./bdd.md) ｜ [TDD](./tdd.md) ｜ [SOP](./sop.md) ｜ 证据：[local-dev/kg-e2e.mjs](../../local-dev/kg-e2e.mjs)、[local-dev/kg-text2cypher-e2e.mjs](../../local-dev/kg-text2cypher-e2e.mjs)、[local-dev/kg-search-e2e.mjs](../../local-dev/kg-search-e2e.mjs) ｜ 单测：[tests/MoAI.KnowledgeGraph.Tests/](../../tests/MoAI.KnowledgeGraph.Tests/)、[tests/MoAI.AIPlugin.Dynamic.Tests/](../../tests/MoAI.AIPlugin.Dynamic.Tests/)

## 自检记录

- 2026-11-09（七）P1 扫尾三项（侧栏编辑跳转/recall 邻居度数/e2e 断言预置）：①详情侧栏操作区补「**编辑实体**」（EditOutlined，canEdit 可见）——跳 `maintenance?step=entities&keyword=<节点名>`；实例页补 `?keyword=` URL 初始化搜索框（此前仅 typeId 跳入支持），画布组件接 `teamId` prop（Detail 传参）供跳转；②查漏补缺——recall-test 响应邻居映射漏 `Degree`（DTO 已有、/search 已带，仅该 handler 映射漏）补齐；③kg-search-e2e 预置 **KGS-S44a/b/c** 断言（邻居行度数字段/recall-test neighborTopK=3 生效且 ≤3 携度数/越界 31→400 含「0-30」文案，node --check 过，50 断言）。**验证**——build 0 error；KG 单测 **103/103**；前端 typecheck/lint 0 error；KG vitest 33/33；**全量 vitest 811/811 一次通过**（并行线收口后总量，负载恢复）；openapi 确认 recall 邻居 degree；syncapi。**待 Memgraph**：kg-search-e2e 全量（含新 S44 三断言）。

- 2026-11-09（六）画布样式规则（@KG-S49，B8，P1 全部收口）：**后端**——knowledge_graph 增 `canvas_style_json`（text 默认空串，`asserts/knowledge_graph_canvas_style.sql` 已执行开发库）；`PUT {id}/canvas-style`（`{captions: {类型id字符串→属性名|""}|null}`：≤50 条/键须可解析 long/值≤100；Handler 校验类型归属本图（不存在 400）、接入图 409、Admin+；null 或空字典清空；序列化 `{captions:{...}}` 中文走 UnsafeRelaxedJsonEscaping）；详情响应增 `canvasStyle`（解析容错，损坏返回 null）。**前端**——设置页新增「画布样式规则」卡（托管图 Admin+，拉 schema 组装 类型+属性清单行；每类型 Select：不显示/类型名(__type__)/属性名；保存空值过滤后 ≤0 条传 null 清除）；Canvas 组件接 `canvasStyle` prop（KnowledgeGraphDetail 传参），renderData 的 labelText 改经 `captionOf`：规则=__type__ 追加「 · 类型名」、=属性名追加「 · 属性值」（属性缺失仅名称）、无规则纯名称——单行拼接规避 G6 多行 label 渲染不确定性。**坑**：Settings 测试 mock 缺新增导出（getKnowledgeGraphSchema 等 5 个）报 No export defined——vi.mock 工厂补齐；node -e 的 bash 反引号/三引号转义连续翻车，多行文本编辑一律 Edit 工具。**验证**——build 0 error；单测 **103/103**（守卫/证据/合并既有覆盖）；KG vitest 33/33（Settings 测试补 mock 后全绿）；typecheck 0 error（并行线已收口 entityCount 中间态）；lint 0 error；openapi 确认 canvas-style 端点与 detail.canvasStyle 契约；syncapi。**待补**：样式规则浏览器走查（配规则→画布 label 变化）。

- 2026-11-08（五）画布问图 Text2Cypher（@KG-S48，B7/D31，P1 收口）：**后端**——POST {id}/text2cypher（{question(≤500), aiModelId?}）：AuthorizeAsync Member 可用 + 接入图 409（接入图问图走 kg_cypher_query 插件）→ 模型三级回退（D30 复用）→ GetSchemaDigestAsync 结构自描述 → 提示词（结构 JSON + $kgId 强制过滤/LIMIT 100/类型清单约束）→ 生成剥围栏取首语句 → **KgCypherReadOnlyGuard**（KG.Core 本地实现——KG.Core 不能反向引用 AIPlugin，与 aiplugin 同规则集：剥注释/字符串/反引号后校验单语句+首关键字白名单+写关键字整词黑名单+8000 上限，字符串字面量内的 CREATE 不误伤）→ ExecuteQueryAsync（$kgId 注入 + 结果侧归属校验，500 行/30s）→ 结果行抽 Guid 值经 GetNodeTypesByIdsAsync 过滤出本图 nodeIds。响应 {ok, cypher, columns, rows, rowCount, truncated, nodeIds, error}。**前端**——工具栏搜索框前 Segmented 双模式（实体/AI 问图），问图模式 enterButton+loading 320px；结果卡 Alert（Cypher code copyable + 行数/截断/高亮提示），nodeIds ≤20 高亮画布（画布上存在的才置态）切回实体模式清卡。**坑**：①守卫 namespace 误写 Models（测试 CS0103，InternalsVisibleTo 早已配好）；②typecheck/lint 各 1 错均属并行线进行中文件（AppAccessSection.test.tsx 语法中间态、embed/widget.ts this 别名），KG 范围零错误。**验证**——build 0 error；KG 单测 **103/103**（新增 KgCypherReadOnlyGuardTests 13 例：只读放行含注释/尾分号/字面量内写词不误伤、写语句/多语句全拒、空与超长拒）；KG vitest 33/33；后端重启 openapi 确认端点；syncapi。**待补**：真实模型问图冒烟（需 Memgraph+模型）。

- 2026-11-08（五）图数据库维护去实时化（@KG-S46 新增、@KG-S47 新增、@KG-S32/S34 退役改写）：按用户反馈「图库维护不做实时监听（后台线程多）/开启前测试/操作时连不上自动报错」三项整改。**① 列表去图库化**——`QueryKnowledgeGraphsCommandHandler` 整体移除逐图实时计数与 3 秒限幅块（`QueryCountsWithTimeoutAsync/QueryCountsAsync` 删除；超时放弃任务在 Neo4j 驱动无界重试里旁路泄漏的根源消除），`KnowledgeGraphItem` 删 `entityCount/relationCount` 属性（仅列表链路引用，已全仓核实）；列表纯业务库查询（设置开关+图谱行），`Enabled` 仍回显供前端横幅拦截；前端卡片计数块与 `statEntities/statRelations` i18n 键删除，`knowledgegraph.disabled` 文案 Neo4j→图数据库。**② 测试连接端点**——新增 `POST /api/knowledge-graph/test-connection`（CQRS 三层：`TestKnowledgeGraphConnectionCommand`（可选覆盖字段+bolt/neo4j scheme 校验）/Handler/Controller root 门禁 403——KG.Api 补 Account.Shared 引用）；Handler **直接查 Settings 表取存储连接值**（坑：`IKnowledgeGraphSettingsService.GetAsync` 未开启时短路不回传连接信息，不能用作回退源）；**临时 driver**（`WithConnectionTimeout(5s)` 不污染 GraphDriverProvider 单例）+ 8 秒 `Task.WhenAny` 整体限幅（驱动不响应取消令牌，沿用既有竞争裁决先例）+ finally Dispose 中断被放弃任务；连接失败返回 `{ok:false,error}` 不抛 503（设置页展示「测试未通过」）；探活语句与建接入图同口径（memgraph `RETURN 1` / neo4j `CALL db.labels()`）。**③ 运行时驱动连接超时**——`GraphDriverProvider` 建 driver 加 `WithConnectionTimeout(10s)`，图库宕机时操作端点快速 503 而非挂起。**④ 设置页开启流程**——保存顺序反转为「先连接项→实测→通过才落 KG_ENABLED=true」，测试未通过不落开关、表单值保留可改后重试（不回弹开关——回弹会隐藏连接字段看不到已填内容）；新增「测试连接」按钮（表单当前值直测无需先保存）；关闭仅写开关。**坑**：Kiota 对保留字 `error` 转义为 `errorEscaped`（封装层映射）。**验证**——dotnet build 0 error（宿主全量因并行线 `dotnet run` 占 bin 偶发 MSB3021 文件锁，无 CS 错误）；KG.Api 单独编译 0 错误；syncapi 重生成；typecheck/lint 0 error；vitest Settings 30/30（新增：开启先连接后开关顺序断言 invocationCallOrder、测试未通过不落开关、测试按钮不触发保存）+ TeamKnowledgeGraphs 13/13；**图库宕机真机实测**（192.168.50.199 不可达）：test-connection 存储值 2.9s ok=false 携带原因、错误地址 2.0s ok=false、非法地址/方言 400、未认证 401、普通用户 403、**列表 44ms 返回**（改造前同场景 3s+且泄漏 N 任务）；kg-e2e 扩 S46a~f/S47a（KG 82→90，AGENTS 同步）。**待 Memgraph 恢复后执行** `node local-dev/kg-e2e.mjs`（含 S46a ok=true）。

- 2026-11-08（四）召回管线升级 + 默认抽取模型（@KG-S44/S45，D28/D30）：**B3 召回升级**——store 新增 `GetNeighborsByDegreeAsync`（`ORDER BY size((m)--()) DESC` 度数优先邻接，检索上下文扩展专用；画布展开仍用名称序 `GetNeighborsAsync` 语义不变）；`GraphSearchService.SearchAsync` 增 `neighborTopK`（0=默认 10 钳制 1~30），邻居**按度数降序**进入上下文且行携带 `Degree`（同邻居多条关系保留「每边一行」既有语义——首版实现误改每邻居一行致既有断言 3≠2 回归，即改回）；文本化改 `GraphSearchTextHelper.BuildHitsText`（预算整段组装：按得分序填充、预算耗尽丢尾部整段置 `TextTruncated`、单段超限前缀截断兜底），`GraphSearchResult/QueryKnowledgeGraphSearchCommandResponse` 增 `TextTruncated`、邻居 DTO 增 `Degree`；`/search` 与 `/recall-test` 命令增 `NeighborTopK`（校验 0~30）。**B5 抽取模型默认**——`knowledge_graph` 增 `extract_model_id`（asserts 已执行开发库）；`PUT {id}/extract-model`（可空清除；校验 启用+团队可用+conversation，复用 `GetTeamUsableModelIdsAsync`）；import-file 的 `aiModelId` 改**可空**（D30 三级回退：请求级 > 图谱默认 > 400）；详情响应/前端类型加 `extractModelId`。**前端**：召回测试表单加「邻居数」（0-30 默认 10，tooltip 说明度数优先）；设置页向量化卡下新增「默认抽取模型」块（对话模型下拉可清空 + 保存）；AI 导入模型下拉预填图谱默认。**坑**：①`IGraphSearchService.SearchAsync` 插参触发 13 处调用/测试 CS1503/CS0854（表达式树禁可选参数——named 实参仍缺第 5 位不行，须补全显式实参）；②测试 mock 未随服务切换到度数版方法 NRE——mock 双方法 Setup；③并行线中间态（QueryKnowledgeGraphsCommandHandler 缺 using、wrapper 删 entityCount 字段）造成构建/typecheck 间歇红——前者补一行 using，后者属其进行中工作不代改（其测试文件自用该字段，待其自行收口）。**验证**——dotnet build 0 error；KG 单测 **90/90**（新增 `GraphSearchTextHelperTests` 3 例：预算内全保留/超预算丢尾段/单段兜底截断）；KG vitest 33/33；lint 0 error（typecheck 仅余并行线 entityCount 中间态 1 错）；后端重启 openapi 确认 extract-model/neighborTopK/degree/textTruncated 契约；syncapi 重生成。**待补**：kg-search-e2e 扩 S44/S45 断言（需 Memgraph）。

- 2026-11-08（三）AI 导入异步状态机 + 证据链幂等合并（@KG-S42/S43，D26/D27，P0a）：**B2 状态机**——新表 `kg_import_job`（status: pending/extracting/parsing/merging/done/failed/cancelled + total/done rows + error/result JSON，`asserts/kg_import_job.sql` 存量库已执行）；`POST {id}/import-file` **契约变更为返回任务 id**（Handler 瘦身：授权+能力+类型存在快检 → 登记 job → `IBackgroundJobClient.Enqueue`）；执行体 `KnowledgeGraphAiImportJob`（[AutomaticRetry(0)] 自管状态机；阶段间 Reload 检测取消；BusinessException→failed(400/409 级) / 其它→failed(500)）；新端点 `GET {id}/import-jobs`（分页倒序）/`GET {id}/import-jobs/{jobId}`/`POST .../retry`（仅 failed/cancelled，重置重入队）/`POST .../cancel`（pending 即时、执行中阶段间隙生效、终态 409）。**B1 幂等合并**——`KgNode` 增可选 `srcJson` 属性（证据数组 {b:批次,d:文档名}，`KnowledgeGraphSourceEvidence` 工具：描述去重拼接截 2000 / 属性目标优先 TryAdd / 证据按批次+文档名去重 FIFO 20，**中文走 UnsafeRelaxedJsonEscaping**——默认编码器转义 \uXXXX，实踩）；AI 导入落库改：节点按（类型+名称）批量匹配命中合并（`UpdateNodeEvidenceAsync`）/未命中新建带证据；边按三元组批量预检跳过（重跑零新建）；向量增量覆盖 新建+合并 节点；结果统计新增 `nodesMerged`。**前端**：AI 导入页签改「发起任务 + 任务历史表（状态 Tag/进度/错误 Tooltip/创建时间）+ 3s 轮询活跃任务 + 失败重试/进行中取消 + 完成提示触发画布刷新」；syncapi 重生成。**坑**：CreateNodeAsync 加可选参数触发 5 处既有 Moq 表达式 CS0854（表达式树禁可选参数）与 Callback/Returns 形参失配——补显式参数；vitest 全量 32 文件失败为开发机负载超时噪音（单跑 49/49+61/61 全过，同 rounds 344 既有模式）。**验证**——dotnet build 0 error；KG 单测 **87/87**（新增 `KnowledgeGraphSourceEvidenceTests` 2 例）；typecheck/lint 0 error；KG vitest 33/33（ImportPage 补任务历史用例）；kg-import-e2e 异步适配由并行线完成（waitJob 轮询段），我追加 **KG-S42b/c 幂等重导与任务治理段**（node --check 过）。**待执行**：kg-import-e2e 全量（需 Memgraph 恢复）。

- 2026-11-08 画布探索工作台 v4（@KG-S35~S40，G6 引擎重写 + 实体合并）：后端三件——① `POST {id}/nodes/merge` 实体合并（`MergeKnowledgeGraphNodesCommand/Handler`：源 1~20 去重且不含目标；重挂边计算=源端点替换目标 + 同三元组（起点,关系,终点）已存在跳过 + 两端均为源的自环丢弃；起止类型约束校验（重挂后目标类型须满足受影响关系类型 SourceTypeId/TargetTypeId，违反 400）；触边 ≥20 未带 `confirm` 409 二段确认；store `MergeNodesAsync` 单事务（SET 目标描述/propsJson → UNWIND 重挂建边 → DETACH DELETE 源）；向量增量目标 upsert + 源批量删除）；② canvas 增 `depth`（1~2，托管图 seed 集再扩一跳受 limit 预算约束）；③ 画布/邻接节点携 `degree`（`size((n)--())`，节点大小映射与侧栏关系计数）。前端画布整体重写（`KnowledgeGraphCanvas.tsx` 编排器 + `canvas/` 模块群）：引擎按 D25 从 `@xyflow/react` 切回 **G6 5.1**（动态 import 路由级分包；布局仍自算 d3-force/环形/网格烘焙进 datum 规避 G6 布局 API 面且保留展开时位置连续；xyflow 已卸载——全仓仅 KG 在用）；六件套工具栏（搜索定位→画布内高亮居中，未命中弹窗转服务端关键字重载/布局三选/深度/上限/缩放/适配/全屏/显示设置-边标签开关）；左下图例=类型计数多选显隐（内存过滤不重载，替代顶部单选 Tag 服务端重载）；**双击展开/收起**（expandedBy 记账，收起回收仅本次引入且无其它连边且未选中/高亮/自展开的节点）；详情侧栏升级（关联关系列表可点跳转邻居 + 度数/已展开标记 + 展开收起/删除入口）；**Ctrl+多选 → 连关系（选中序=起终点）/合并对话框**（目标单选+差异摘要+Popconfirm 不可逆二次确认，`confirm:true` 直传）；右键菜单（画布建实体/节点展开删除/边删除）经 antd Dropdown 定位实现；召回测试「在画布查看」经 `?highlight=` 联动（缺失节点邻接补拉 ≤10 后高亮聚焦）；导入页疑似重复表加「去合并」列（新行 id 从逐条结果按 index 对齐、existing 用 matchNodeId / inbatch 用 matchIndex 行 id → `?merge=` 深链预选开合并，id 不齐时不渲染按钮）。验证——dotnet build 0 error（.builds/kg 输出验证）；KG 单测 **85/85**（新增 `MergeKnowledgeGraphNodesCommandHandlerTests` 4 例：重挂计算/自环丢弃/描述合并/向量增量 Verify、约束 400、≥20 触边 409 与 confirm 放行、源缺失 404）；syncapi 重生成（merge/depth/degree 契约确认在 openapi）；vitest 全仓 **795/795**（Detail 测试加 `@antv/g6` 桩——jsdom 无 canvas 2d，G6 渲染管线 `clearRect` 炸异步不可 try/catch，必须 vi.mock；RecallTest 测试补 MemoryRouter——新增 useNavigate 需 Router 上下文）；typecheck/lint 0 error。kg-e2e 已扩 S37/S38/S40 断言段（独立图自包含：造数→degree 断言 b1/a2=2→depth 1/2/越界 400→merge 约束 400/含目标 400/正例 rehung=0+自环=1+删源=1/合并后 2 节点 1 边/描述合并；node --check 过），**待 Memgraph 恢复后执行** `node local-dev/kg-e2e.mjs http://127.0.0.1:5000`。

- 2026-11-08（二）浏览器真实走查（Edge headless @ playwright-cli，admin → aa 团队 → 云途业务图谱 /kg/237/canvas）：**G6 真实浏览器运行时验证通过**——4 层 canvas 元素创建、工具栏全部件渲染（搜索定位框/力导向·环形·网格/1·2 跳/上限/重新加载/缩放/适配/全屏/设置弹层含边标签开关）、左下图例面板带类型计数（产品/功能/会员版本，schema 走 PG 不依赖图库）、布局切换与设置弹层交互无 JS 错误、无白屏无错位；控制台仅存量 antd v5-React19 兼容告警。**走查揪出并修复一个缺陷**：`load` 的 useCallback 依赖含 `renderData`（其依赖 layout/hiddenTypes/边标签），切换布局/图例过滤/设置均触发整图重新请求（图库不可用时 503 刷屏、可用时浪费重拉）——改 `renderDataRef` 解耦（load 依赖收敛为 [graphId, limit, depth]），复测布局切换两次 **0 次新增 canvas 请求**。图库（192.168.50.199:7687）仍宕机：节点级交互（双击展开/搜索高亮/多选合并）无法在真实数据上走查；画布 503 转圈属 round 345 已记录的驱动重试挂起（非本次回归）。复验：vitest 全仓 795/795（ImportPage 测试补 MemoryRouter——去合并列新增 useNavigate）、typecheck/lint 0 error。

- 2026-11-08 图库不可用时列表限幅降级 + 列表加载态（@KG-S34）：用户报「团队知识图谱不能新建」——实为图数据库（`KG_URI=bolt://192.168.50.199:7687`，Memgraph 未运行、端口拒绝）不可用时，`QueryKnowledgeGraphsCommandHandler` 逐图同步统计数量，Neo4j 驱动对连接失败**无界重试**（会话 API 5.28 不收 CancellationToken）把 `GET /api/knowledge-graph/list` 拖死 135 秒（复现实测）；挂起期间前端停留初始态（items=[]、myRole=null）→ 新建按钮（Admin+ 才渲染）缺席 + 空态文案，误似「不能新建」。修复：① handler 统计并发化（Task.WhenAll）+ 单图 `Task.WhenAny` 限幅 3 秒（`QueryCountsWithTimeoutAsync`，超时降级 null 计数、放弃任务异常旁路吞掉）——图库拒绝连接实测 **135s → 3.1s**（3 图各 1s 超时余量）；② `TeamKnowledgeGraphs` 加 `loading` 态包 Spin（加载中不再与空态混淆，admin 可见按钮随数据到位渲染）。踩坑：Neo4j.Driver 5.28.0 `ExecuteRead/WriteAsync` 无 cancellationToken 重载（编译实测 CS1739），取消只能外层任务竞争裁决。顺带验证：托管空白图创建 200/104ms（创建链路本身无恙）；删除接口 503（先清图库再软删，图库不可用被拒属预期）。验证——dotnet build 0 error；vitest TeamKnowledgeGraphs **13/13**；typecheck/lint 0 error；真机复测列表 3.1s 返回 myRole=2。

- 2026-11-08 侧栏头部身份区改 SiderHeader（返回与图名分行）：上一轮「BackLink+图名挤同一行」按用户反馈再调整——参照首页侧边栏头部两段式：上段图谱头像+图名、下段整宽「← 返回知识图谱」按钮，与团队/应用工作台、知识库详情同构。`KnowledgeGraphDetail` 换 design-system 新组件 `SiderHeader`（avatarPath 经 resolveStorageUrl，ClusterOutlined 兜底）；加载分支（graph===null）顶部仍单独保留 BackLink 兜底。design-system components.md BackLink 位置规则改「无分区菜单页」+ 新增 SiderHeader 章节。vitest `KnowledgeGraphDetail.test.tsx` **5/5**（返回入口断言 link→button）；typecheck/lint 0 error。

- 2026-11-07 编辑弹窗上传头像（@KG-S33）：`TeamKnowledgeGraphs` 编辑弹窗原只有名称/简介（头像 `Form.Item` 被 `!editing` 包住仅新建可见，编辑提交路径也不登记头像）。改弹窗头像区新建/编辑共用：编辑态预览初始化为已有 `avatarPath`（`resolveStorageUrl` 回显），保存成功后串联既有 `POST {id}/avatar` 直传登记（与创建同口径：登记失败不阻断保存，可去设置页补传）；`resetAvatar` 仅回收 blob 预览地址（编辑态预览为存储地址，`URL.revokeObjectURL` 对其无害但语义上收紧）。零后端改动、无需 syncapi。vitest TeamKnowledgeGraphs 11→13（新增：编辑弹窗展示头像控件且回显当前头像、编辑选图保存后按 kgId 登记头像）；typecheck/lint 0 error。
- 2026-11-07（二）详情页返回入口收进左栏顶部：`KnowledgeGraphDetail` 原把「← 返回知识图谱 + 图名」独占一行放在左 Sider + 右 Content 两栏布局之上，右侧内容区头部留大片空白（与 WikiDetail 同构问题，同轮整改）。改收进左 Sider 顶部（BackLink 包 `flexShrink: 0`、图名 `flex: 1`+`ellipsis`），与右侧内容同行；加载分支（graph===null）在顶部单独保留返回入口避免短暂丢导航。design-system components.md BackLink 位置规则同步（三级分区页 → Sider 顶部，禁止位置改「全局侧边栏内」）。vitest `KnowledgeGraphDetail.test.tsx` **5/5**（新增 within(aside) 结构断言：返回链接与图名必须在左侧菜单栏内）；typecheck/lint 0 error。
- 2026-11-07 列表卡片资源数量与布局同构（@KG-S32）：团队图谱卡片与知识库卡片同构（头像+名称+元信息行 / 两行截断描述 / 分隔线页脚 创建时间+操作），并新增图谱资源数量——后端 `KnowledgeGraphItem` 增 `entityCount/relationCount`（int?，图库不可用/能力未开启/统计失败为 null），`IKnowledgeGraphStore` 增 `CountNodesAsync/CountEdgesAsync`（`MATCH (n:KgNode {kgId}) RETURN count(n)` 同款边计数），`QueryKnowledgeGraphsCommandHandler` 逐图按模式取数（托管直查；接入图经 `IKnowledgeGraphIntrospectionCache`（5 分钟缓存）汇总标签计数；`settings.Enabled=false` 直接置空避免逐图连接超时；单图失败 LogWarning 降级不阻断列表）；syncapi 重生成 Kiota 客户端；前端卡片元信息行「N 实例 · M 关系」（`knowledgegraph.statEntities/statRelations` zh/en），接入徽标同行展示，列宽补 `xxl={4}` 与知识库一致。vitest TeamKnowledgeGraphs 11/11（含 `12 实例 · 34 关系` 断言）；dotnet build 0 error；typecheck/lint 0 error；真机冒烟 admin 登录拉 `/api/knowledge-graph/list` 回显 `ce1=11/15`、`云途业务图谱=9/11`、空图 `0/0`。
- 2026-11-07 团队图谱列表工具栏搜索（@KG-S31）：`TeamKnowledgeGraphs` 原「新建按钮」孤立工具栏改 `PageToolbar` 三段式（与知识库列表同款）：搜索框（关键字子串匹配名称/描述不区分大小写，回车或「查询」触发）+「查询」+ 共 N 条（按过滤后数量回显）+ 竖分隔线 + 新建知识图谱（Admin+，未开启能力禁用）。客户端过滤不改 `getKnowledgeGraphs` 接口；空态补齐——无数据 Empty（`knowledgegraph.empty`，原列表为空时不渲染任何提示）与无命中 Empty（`knowledgegraph.filterEmpty`）区分；i18n 增 `knowledgegraph.listSearchPlaceholder/filterEmpty`（zh/en；`searchPlaceholder` 已被画布/实例节点搜索占用故另立键）。vitest TeamKnowledgeGraphs 10→11 例（搜索：输入不查询不过滤/查询过滤/筛选空态）；typecheck/lint 0 error；全仓 vitest 因开发机负载偶发超时（失败文件逐轮漂移且均非本次改动，单跑全过——WikiDocumentDetail/AppWorkspace/AppChat/AppConfigSection/AdminAccessApps/Classify 六文件单独复跑 99/99，与 tdd 记录的既有负载噪音模式一致）。
- 构建：`dotnet build src/MoAI/MoAI.csproj` → **0 错误**（2026-09-15，v2.1）
- 单测：`dotnet test tests/MoAI.KnowledgeGraph.Tests/MoAI.KnowledgeGraph.Tests.csproj` → **PASS 40/40**（2026-09-15，v2.1 新增接入图 refresh 透传与 changes 映射；存量覆盖模板映射、探活 / 内省、能力门禁、角色判定、只读拦截、类型引用拦截、分页钳制、删图清库分支）
- E2E：`node local-dev/kg-e2e.mjs http://127.0.0.1:5000` → **PASS 47/47**（2026-09-15，v2.1 新增 S17 接入图画布（entityLabel/relationName/label 过滤）、S18 接入图邻接（elementId、404）、S19 内省缓存/强制刷新）。脚本无图数据库时打印 `SKIP` 并退出码 0（CI 友好）。
- 前端：`npm run typecheck` 0 错误；`npm run lint` 0 错误（3 个 skills 既有警告）；`npm run test` **258/258**（2026-09-15，syncapi 重生成后全量通过）。
- v2.2 头像：单测 **42/42**（新增 `UpdateKnowledgeGraphAvatarCommandHandlerTests` 2 例）；E2E `node local-dev/kg-e2e.mjs http://127.0.0.1:5020` → **PASS 52/52**（2026-09-15，新增 S20：真实存储直传→设置→详情/列表回显→未登记 404）。
- v2.3 模型属性：单测 **43/43**（新增 `QuerySchema_ReturnsEntityTypeProperties`）；E2E → **PASS 59/59**（2026-09-15，新增 S21 属性全链路 7 项；顺带修复 v1 遗留缺陷：Update 节点/边/实体类型/关系类型 4 个命令 validator 校验路由字段导致所有编辑操作 400，移除路由字段规则并新增 S21g 编辑覆盖断言）。
- 外部开放接口：`node local-dev/kg-external-e2e.mjs http://127.0.0.1:5210` → **PASS 58/58**（2026-09-15，KX-01~KX-08：应用 token 换取、团队级列表、跨团队 404、类型/节点/边 CRUD 与分页邻接、批量整批拒绝、connected 409、仅应用 token）；无图数据库或 `KG_ENABLED=false` 时打印 SKIP 并退出码 0。
- 2026-09-26 知识图谱读/写范围（@KX-09）：`/external/knowledge-graph` 读接口要求 token/直连上下文 `kg_read`（64）、写接口要求 `kg_write`（128）；`access_app` 存量 `|= 192` 补 KG 全量、DEFAULT 230（asserts/access_app_kg_scopes.sql，开发库已执行），未传 scopes 的接入默认全量（`AccessAppDefault`）。kg-external-e2e 扩至 **67/67**（KX-09a~i：只读 token 读放行/写 403/跨资源组隔离/直连读写分档/补写刷新放行）；范围模型见 [../gateway/sdd.md](../gateway/sdd.md) 资源分组表。
- 2026-09-27 外部批量导入与按 key 维护（@KX-10~KX-12）：新增 `POST {kgId}/import`（类型名引用 + `autoCreateTypes` 自动建类型 + 节点业务 key 幂等 upsert（key 未命中回退类型+名称并收养 key）+ 边端点按 nodeId/key/名称引用 + **逐条失败报告不阻断** + upsert 边按（起点,关系,终点）去重）、`POST {kgId}/nodes/batch-delete`（按 key 批删，≤500，DETACH 级联 + 向量删除增量）、`GET {kgId}/nodes/by-key/{key}`（同步核对）。图库 `KgNode` 增可选 `key` 属性（无 schema 迁移）。踩坑：`UNWIND ... DETACH DELETE n RETURN n.id` 删除后访问节点属性在 memgraph 方言报错（改两步：先读 id 再裸删除）；UNWIND 的 `LIMIT` 作用于整个结果集而非每行（三元组存在性查询误加 LIMIT 1 漏判）。
- 2026-09-27 召回测试菜单（对齐知识库召回测试）：新端点 `POST {id}/recall-test`（`QueryKnowledgeGraphRecallTestCommand`，IUserIdContext + Controller SetUserContext——路由回填命令手动注入用户上下文坑的又一实例；团队成员级）复用 GraphSearchService，AI 优化问题/AI 回答语义与 wiki 召回测试一致（DisableThinking、优化失败即抛、无命中跳过回答）；前端新增「召回测试」菜单页（managed only，双栏表单 + AI 回答卡/优化前缀/命中项含实体类型与一跳邻居）。E2E kg-search-e2e 扩 KGS-S10a~f（桩补 chat 分支）**46/46**；前端 KG vitest 29/29（RecallTest 3 例 + Detail 菜单断言）、typecheck/lint 0（Canvas WIP 除外）。
- 2026-09-27 导入疑似重复检测（向量相似度）：`KnowledgeGraphImportPayload.detectDuplicates`（默认开启，页面开关可关）→ 导入后对**新建节点**批量向量化（「名称
描述」契约）→ ①向量库检索对照图谱已有节点（top3/行）②批内两两余弦；阈值 0.85（`DuplicateScoreThreshold`）、每图每次最多检查 100 行、检测结果随响应 `duplicateSuspects` 返回（kind=existing/inbatch + score + matchName/matchNodeId/matchIndex）；**任何失败仅记日志不影响导入**（未配置向量化模型/模型不可用静默跳过）。前端导入页加「疑似重复检测」开关（默认开）与疑似重复结果表。**E2E 数据坑（实踩）**：3-gram 桩是字符级相似度——「同名+2」余弦仅 0.815/0.810（低于 0.85），测试数据须用文本全同（同名不同类型/同描述）才能稳定过阈；产品阈值 0.85 面向语义 embedding 不改。kg-import-e2e 扩至 **20/20**（KG-S29j~m）；回归 kg-external **101/101**、kg-mcp **35/35**；前端 vitest 26/26（ImportPage 断言含疑似重复渲染）。
- 2026-09-27 导入页下载示例 + MCP 菜单：JSON 导入表单加「下载示例 JSON」（Blob 下载可导入的完整示例，含 key/类型名引用/属性/边两种端点引用形态）；详情页新增「MCP」菜单（managed only，镜像 wiki WikiMcp：serverinfo serviceUrl + /api/teamres/knowledge-graph/{kgId}/mcp 地址框复制 + 四只读工具清单 + kg_mcp 鉴权提示，`KnowledgeGraphMcpPage`）；KG vitest **26/26**（Detail MCP 菜单断言 managed 有/connected 无 + ImportPage 下载示例 1 例）、typecheck/lint 0（Canvas WIP 除外）。
- 2026-09-27 导入入口收敛：移除维护页「录入实例」步骤的 AI 导入按钮与 `KnowledgeGraphImportModal`（组件删除、i18n import.title/button/cancel/close 键清理 zh/en），AI 导入统一走「导入」菜单页内嵌表单；KG vitest 25/25、typecheck/lint 0（Canvas WIP 除外）。
- 2026-09-27 导入页（新增「导入」菜单 + /import-json）：内部 `POST {id}/import-json`（仅托管图 Admin+）容错解析导入页 JSON 文本（AllowTrailingCommas+注释跳过，解析错误带行定位）→ 载荷校验（KnowledgeGraphImportPayloadValidator，与外部 /import 共用规则集；页面控件 mode/autoCreateTypes/validateOnly 覆盖 JSON 内同名字段）→ **KnowledgeGraphDataImportService 共享管线**（外部 /import handler 同步瘦身为委托，外部入口行为零变化）。前端新增导入菜单页（Tabs：AI 智能导入内嵌表单 + JSON 导入粘贴/上传/预检/逐条结果表），syncapi 重生成。E2E kg-import-e2e 扩 @KG-S29a~i **16/16**；回归 kg-external **101/101**；前端 KG 目录 vitest **25/25**（新增 ImportPage 3 例 + Detail 导入菜单断言）、typecheck/lint 0（Canvas WIP 除外）。
- 2026-09-27 知识图谱 MCP 服务器（@KGM-S1~S7）：端点 `/api/teamres/knowledge-graph/{kgId}/mcp`（streamable HTTP 无状态），新增 `kg_mcp` scope 位（256，接入/团队 key 均可勾选；asserts/access_app_kg_mcp_scopes.sql 存量 `|=256`、DEFAULT 486，开发库 219 条已回填）；四只读工具 list_knowledge_graphs/get_knowledge_graph_schema/search_knowledge_graph_nodes/search_knowledge_graph_recall（召回复用 GraphSearchService，未配向量化返回 skippedHint 不报错、接入图 409）。与知识库 MCP 共享 McpServerOptions——**工具域隔离**经 `KnowledgeGraphMcpToolGate`（AddListToolsFilter/AddCallToolFilter 按请求路径过滤；SDK spike 确认 filter 为中间件形态 `next => async (RequestContext<TParams>, ct)`、工具名在 `Params.Name`；未知工具放行 SDK 原生 -32602，避免破坏 WM-S6a 基线）；中间件 GetRequiredResourceScope 对 `/mcp` 短路（MCP 端点 403 insufficient_scope 曾因落入 kg_write 分档）。E2E `kg-mcp-e2e.mjs` **35/35 零 SKIP**（本地桩 embedding）；回归 wiki-mcp 30/30、kg-external 101/101、team-apikey-scope 49/49。
- 2026-09-27 维护步骤条去重序号（前端）：antd Steps 自带圆圈序号，i18n guide.step1~3 文案里的手写 ①②③（en 为 "1. "）与之重复——去掉前缀；无引用的 guide.step4 键删除（zh/en）；Detail 测试断言同步。vitest KG 22/22。
- 2026-09-27 维护页工具栏合并（前端）：录入实例/连接关系两步骤的筛选栏原在 DataTable 外部、刷新按钮在 DataTable 内置 toolbar 行，导致刷新独占一行——筛选栏（新建/AI 导入/类型筛选/搜索/节点 Tag）整体迁入 `DataTable.toolbar` prop 与刷新同行；vitest KG 22/22、eslint 0 error、typecheck（除 Canvas 并行 WIP）0。
- 2026-09-27 类型弹窗美化（前端）：实体/关系类型编辑弹窗加宽 560、颜色 `Input[type=color]` 换 antd `ColorPicker`（showText/allowClear，`getValueFromEvent` 归一 hex 字符串兼容后端可空 color 契约）、名称+颜色同行、关系起止类型并排、描述 TextArea autoSize、属性行改卡片式两行布局（名称 flex 加宽/说明独占一行，底色取 `token.colorFillQuaternary`）。vitest KG 目录 22/22、改动文件 eslint 0 error（4 warning 为存量非组件导出提示）、typecheck 除并行画布 WIP 外 0。
- 2026-09-27 维护页「模型」步骤微调（前端）：去掉「模型是什么？」说明 Alert（i18n schema.introTitle/introDesc zh/en 键删除），实体类型/关系类型区块标题加数量 Tag——关系类型维护区（新增/编辑/删除）本就在实体类型表格下方，去掉 Alert 后首屏直达。vitest KG 目录 22/22（Schema/Detail/Entities/Relations/Maintenance）、改动文件 eslint 0（typecheck 唯一错误在 KnowledgeGraphCanvas.tsx:733，系并行画布 WIP 与本改动无关）。
- 2026-11-04 团队外部接入大改（@KX-09/@KX-11 改写去范围化语义、新增 @KX-15a~e、@KGM-S1e 改越域资源 key 403、新增 @KGM-S8）：moai-ac- 接入 key 全量读写放行（kg_read/kg_write/kg_mcp 概念删除，接入创建/更新不再收 scopes），图资源 key（moai-tr-，`/api/team-resource-key`）包围圈——本图 REST+MCP 放行、团队级 KG list 经 OnlyGraphId 只见绑定图、越 wiki/越圈 403 insufficient_scope、重置/停用/删除即时生效。E2E kg-external **101→110/110**、kg-mcp **35→36/36**；回归 teamres-key-e2e **31/31**；设计见 [../app/sdd.md](../app/sdd.md)「增量设计（2026-11-04）」与 [../gateway/external-api-scope-system.md](../gateway/external-api-scope-system.md)。
- 2026-09-27 第二批：按 key 同步闭环 + 导入预检（@KX-13~KX-14）。新增 `POST {kgId}/nodes/keys/list`（分页枚举已落 key 节点，全量比对找删除差集）、`POST {kgId}/edges/batch-delete`（按关系类型 + 端点引用删边，同三元组平行边全删，端点未命中按行失败、无边幂等计 0；建议先删边后删点）、`import` 增 `validateOnly`（只读预演：不建类型不写图库不发向量，created 行 id 恒空）；单节点 `POST /nodes`、`PUT /nodes/{id}` 增可选 `key`（创建即落 key / PUT 附 key 收养）。KG 单测 **81/81**（Moq 表达式树不能省可选参数——Setup/Callback/Returns 需同步补 `string?` 形参）；E2E 扩至 **101/101**（KX-13a~j / KX-14a~g；注意 `WhenWritingNull` 会省略 null 字段，断言可空字段用 `== null`）。
- v2.4 设置页体验：vitest **303/303**（Settings 5/5：卡片折叠/展开、图数据库类型保存、关闭不提交连接信息）；typecheck 0、lint 0 错误（2026-09-17）。
- v2.5 物流模板 + 默认图览：单测 **43/43**（模板断言改物流运输 + 预置属性落库断言；顺带修复脚手架重生成后测试种子缺 `Properties` 非空赋值的 7 例存量失败）；E2E `node local-dev/kg-e2e.mjs http://127.0.0.1:5310` → **PASS 61/61**（2026-09-21，5310 独立实例，KG-S2 扩为 a…h 含航段预置属性断言，图库可达零 SKIP）；前端 typecheck 0、lint 0 错误（8 warning 存量）、vitest **424/424**。
- v2.6 建图弹窗上传头像：前端 typecheck 0、lint 0 错误（8 warning 存量）、vitest **429/429**（TeamKnowledgeGraphs 扩至 10 例：选图创建登记/登记失败不阻断/未选不调用）。零后端改动——创建成功后前端串联既有 `POST {id}/avatar` 登记（KG-S20 链路），无需 syncapi。
- v2.7 模板一次性预置示例实例与关系：单测 **45/45**（新增 `Handle_WithTemplate_SeedsExampleNodesAndEdges` 11 节点/15 边与端点映射断言、`Handle_WithTemplate_WhenSeedingFails_PurgesGraphAndRollsBack` 失败清理回滚）；E2E `node local-dev/kg-e2e.mjs http://127.0.0.1:5000` → **PASS 65/65**（2026-09-21，KG-S2 扩至 a…k：13 节点/16 边总数、示例航段属性、建图即得完整图览，两跑一致）。
- v2.8 图览交互（拖动/详情/关系标签）：前端 typecheck 0、lint 0 错误（8 warning 存量）、vitest **429/429**；浏览器实测（走查团队物流图 105，G6 真渲染）：节点拖动保持、节点详情抽屉（类型/属性）、边详情抽屉（关系名/起止）、边关系名标签、两段式点击建边弹窗起止回显全过。顺带修复两处前端缺陷：①G6 v5 点击判定误用 `target.type`（恒 undefined）致点击节点展开邻接自 v2 起实际失效，改 `targetType`；②Kiota 把节点 `properties` 字典收进 `additionalData` 致属性读取恒空（实例列表同受影响），`flattenNodeProperties` 归一化。
- v2.9 AI 导入文件生成图谱：单测 **49/49**（新增 `KnowledgeGraphImportParserTests` 4 例）；E2E `node local-dev/kg-import-e2e.mjs http://127.0.0.1:5000` → **PASS 7/7**（桩模型：模板图建图、model-options、非法 objectKey 400、导入 2 节点 1 边入图、画布计数、航段属性、空白模板图 409）；真实模型冒烟（Qwen3.5 9B 导入 txt：新增 6 节点 6 边，画布 11→17，航段属性完整）；前端 typecheck 0、lint 0 错误、vitest **430/430**；浏览器走查导入弹窗与结果展示。踩坑：`string.Equals(x, y, StringComparison)` 不可翻译为 SQL（model-options 首版放查询内 500，改为内存过滤）；GLM/DeepSeek 渠道对 `DisableThinking`（reasoning_effort=none）报 400001/402 为渠道侧问题，导入对模型无思考链要求时建议选 Qwen 系（2026-09-21）。
- Text2Cypher 查图插件（KT）：单测 `dotnet test tests/MoAI.AIPlugin.Dynamic.Tests/MoAI.AIPlugin.Dynamic.Tests.csproj` → **PASS 36/36**（2026-09-22，`CypherReadOnlyGuardTests` 29 例 + `KgCypherQueryPluginParamsTests` 7 例）；E2E `node local-dev/kg-text2cypher-e2e.mjs http://127.0.0.1:5000` → **PASS 15/15**（2026-09-22，真实后端 + Memgraph）。
- 图检索消费层（SP-A）：四套件单测 → KG.Tests **PASS 81/81**、App.Tests **PASS 39/39**、Workflow.Tests **PASS 77/77**、AI.Core.Tests **PASS 73/73**（2026-09-22，新增 `GraphSearchServiceTests` 10 例、`KgEmbeddingServiceTests` 11 例、`UpdateKnowledgeGraphEmbeddingConfigCommandHandlerTests` 5 例、`DeleteKnowledgeGraphCommandHandlerTests` 3 例、`GraphAppToolProviderTests` 7 例、`KnowledgeGraphSearchNodeTests` 7 例、`GraphSearchTextHelperTests` 8 例、App 侧 GraphIds 绑定 4 例）；E2E `node local-dev/kg-search-e2e.mjs http://127.0.0.1:5000` → **PASS 40/40**（2026-09-22，本地 OpenAI 兼容桩 embeddings，真实后端 + Memgraph + RabbitMQ + pgvector，零 SKIP）；前端 typecheck/lint 0 错误。

## 映射表

| 场景 | 验证物（单测） | E2E（待跑） | 结果（日期） |
|---|---|---|---|
| @KG-S1 | `CreateKnowledgeGraphCommandHandlerTests.Handle_WhenEnabledAndAdmin_CreatesGraph`（mode=managed、database=null） | kg-e2e.mjs#KG-S1a/b（b=名称含特殊字符 400，2026-11-07 扩） | 单测 PASS；E2E 66/66（2026-11-07，含 KG-S1b） |
| @KG-S2 | `CreateKnowledgeGraphCommandHandlerTests.Handle_WithTemplate_MapsRelationTypeIdsToSavedEntityTypes`、`Handle_WithTemplate_SeedsExampleNodesAndEdges`、`Handle_WithTemplate_WhenSeedingFails_PurgesGraphAndRollsBack`；`KnowledgeGraphTemplatesTests` | kg-e2e.mjs#KG-S2a…k | 单测 PASS；E2E 未执行 |
| @KG-S3 | `CreateKnowledgeGraphCommandHandlerTests.Handle_WhenNameDuplicated_Throws409` | kg-e2e.mjs#KG-S3 | 单测 PASS；E2E 未执行 |
| @KG-S4 | `KnowledgeGraphSchemaCommandHandlerTests`（Create/UpdateRelationType 校验、Delete 引用拦截、QuerySchema 回显排序） | kg-e2e.mjs#KG-S4a…c | 单测 PASS；E2E 未执行 |
| @KG-S5 | `KnowledgeGraphNodeEdgeCommandHandlerTests.CreateNode_WithEntityTypeNotInGraph_Throws400` | kg-e2e.mjs#KG-S5a/b | 单测 PASS；E2E 未执行 |
| @KG-S6 | `KnowledgeGraphNodeEdgeCommandHandlerTests.CreateEdge_WithMissingEndpointNode_Throws400`、`CreateEdge_WhenEndpointViolatesRelationConstraint_Throws400`、`CreateEdge_HappyPath_ReturnsEdgeId` | kg-e2e.mjs#KG-S6a…c | 单测 PASS；E2E 未执行 |
| @KG-S7 | `KnowledgeGraphNodeEdgeCommandHandlerTests.QueryNodes_ClampsPageSizeAndMapsItems` | kg-e2e.mjs#KG-S7a…d | 单测 PASS；E2E 未执行 |
| @KG-S8 | `KnowledgeGraphSchemaCommandHandlerTests.DeleteEntityType_WithNodesPresent_Throws409`、`DeleteEntityType_ReferencedByRelationType_Throws409` | kg-e2e.mjs#KG-S8 | 单测 PASS；E2E 未执行 |
| @KG-S9 | `DeleteKnowledgeGraphCommandHandlerTests.Handle_WhenManaged_PurgesGraph` | kg-e2e.mjs#KG-S9a…c | 单测 PASS；E2E 未执行 |
| @KG-S10 | `CreateKnowledgeGraphCommandHandlerTests.Handle_Connected_WhenProbeFails_Throws400` | kg-e2e.mjs#KG-S10 | 单测 PASS；E2E 未执行 |
| @KG-S11 | `CreateKnowledgeGraphCommandHandlerTests.Handle_Connected_WhenProbeSucceeds_PersistsModeAndDatabase`、`KnowledgeGraphSchemaCommandHandlerTests.QuerySchema_WhenConnected_UsesIntrospection` | kg-e2e.mjs#KG-S11a…f | 单测 PASS；E2E 未执行 |
| @KG-S12 | `KnowledgeGraphAuthorizerTests.AuthorizeManagedAsync_WhenConnected_Throws409`、`KnowledgeGraphNodeEdgeCommandHandlerTests.CreateNode_OnConnectedGraph_Throws409` | kg-e2e.mjs#KG-S12a/b | 单测 PASS；E2E 未执行 |
| @KG-S13 | —（画布查询走真库 Cypher，拦截层由 Detail 页 vitest 覆盖调用） | kg-e2e.mjs#KG-S13a/b | **E2E PASS 40/40（2026-09-14）** |
| @KG-S14 | —（同上） | kg-e2e.mjs#KG-S14a/b | **E2E PASS（2026-09-14）** |
| @KG-S15 | `KnowledgeGraphNodeEdgeCommandHandlerTests.CreateNode_AsMember_Throws403`（Member 全只读） | @manual（需 Member 账号，浏览器走查见 sop §5） | 单测 PASS（2026-09-14） |
| @KG-S16 | `CreateKnowledgeGraphCommandHandlerTests.Handle_WhenNameDuplicatedAcrossTeams_Throws409` | kg-e2e.mjs#KG-S16a | 单测 PASS；**E2E PASS（2026-09-14）** |
| @KG-S17 | —（接入图画布走真库 Cypher） | kg-e2e.mjs#KG-S17a/b/c | **E2E PASS 47/47（2026-09-15）** |
| @KG-S18 | —（接入图邻接走真库 Cypher） | kg-e2e.mjs#KG-S18a/b | **E2E PASS（2026-09-15）** |
| @KG-S19 | `KnowledgeGraphSchemaCommandHandlerTests.QuerySchema_WhenConnectedWithRefresh_ForwardsRefresh`（refresh 透传 + changes 映射） | kg-e2e.mjs#KG-S19a/b | 单测 PASS；**E2E PASS（2026-09-15）** |
| @KG-S20 | `UpdateKnowledgeGraphAvatarCommandHandlerTests`（未登记 404、登记后落库） | kg-e2e.mjs#KG-S20a…e | 单测 PASS；**E2E PASS 52/52（2026-09-15）** |
| @KG-S21 | `KnowledgeGraphSchemaCommandHandlerTests.QuerySchema_ReturnsEntityTypeProperties` | kg-e2e.mjs#KG-S21a…g | 单测 PASS；**E2E PASS 59/59（2026-09-15）** |
| @KG-S22 | `ui/src/pages/settings/__tests__/Settings.test.tsx`（卡片默认展开/折叠、图数据库类型保存） | —（纯前端布局，浏览器走查） | vitest PASS 5/5（2026-09-17） |
| @KG-S23 | `ui/src/pages/knowledgegraph/__tests__/KnowledgeGraphDetail.test.tsx`（托管图菜单 图览/维护/设置、接入图 图览/模型/设置、默认图览调 canvas 接口、旧路径 /entities 重定向维护步骤）、`ui/src/pages/teams/knowledgegraph/__tests__/TeamKnowledgeGraphs.test.tsx` | —（纯前端行为） | vitest PASS 5/5（2026-09-27） |
| @KG-S24 | `ui/src/pages/teams/knowledgegraph/__tests__/TeamKnowledgeGraphs.test.tsx`（创建选图→建图→登记、登记失败不阻断、未选不调用） | —（创建弹窗前端编排，复用 KG-S20 直传登记链路） | vitest PASS 3/3（2026-09-21） |
| @KG-S33 | `ui/src/pages/teams/knowledgegraph/__tests__/TeamKnowledgeGraphs.test.tsx`（编辑弹窗展示头像控件且回显已有头像、编辑选图保存后登记） | —（编辑弹窗前端编排，复用 KG-S20 直传登记链路） | vitest PASS 13/13（2026-11-07） |
| @KG-S34 | `QueryKnowledgeGraphsCommandHandler`（限幅方案已退役：列表不触碰图库，`QueryCountsWithTimeoutAsync` 删除）+ `TeamKnowledgeGraphs.test.tsx`（加载回归 13/13） | kg-e2e.mjs#KG-S47a + 真机：图库宕机列表 **44ms** 返回（2026-11-08 实测；改造前同场景 3s+ 且每请求泄漏 N 个驱动重试任务） | PASS（2026-11-08 真机实测） |
| @KG-S25 | —（G6 画布交互无 jsdom 渲染，浏览器走查覆盖：拖动/节点详情/边详情/边标签/两段式建边） | @manual（走查记录见 sop §5） | PASS（2026-09-21 浏览器实测） |
| @KG-S27 | `ui/src/pages/knowledgegraph/__tests__/KnowledgeGraphDetail.test.tsx`（画布 limit 默认 200 调用）+ `KnowledgeGraphCanvas.tsx` 实现（React Flow + d3-force，jsdom 直接渲染无需 mock） | @manual（浏览器走查：图览无步骤条/导入按钮、画布撑满、缩放/适应/全屏、截断提示） | PASS（2026-09-27 浏览器实测 Edge，React Flow 版） |
| @KG-S28 | `ui/src/pages/knowledgegraph/__tests__/KnowledgeGraphDetail.test.tsx`（维护步骤条渲染、?step= 路由、旧路径重定向后「新建实例」入口） | —（纯前端行为） | vitest PASS（2026-09-27） |
| @KG-S26 | `KnowledgeGraphImportParserTests`（JSON/fence/snake_case/缺字段/非文本 4 例） | local-dev/kg-import-e2e.mjs#KG-S26a…g（桩模型；v4.1 改异步：登记任务 → waitJob 轮询终态断言） | 单测 PASS；E2E 待 Memgraph（异步适配段 2026-11-08 就绪） |
| @KG-S42 | —（状态机由 `KnowledgeGraphAiImportJob` 实现，Moq 难以覆盖 Hangfire 装载路径） | local-dev/kg-import-e2e.mjs#KG-S42c1…c3（列表含任务/done 重试 409/done 取消 409） | E2E 待 Memgraph；dotnet build + 单测 87/87（2026-11-08） |
| @KG-S43 | `KnowledgeGraphSourceEvidenceTests`（描述去重拼接/截断、证据按批次+文档名去重、FIFO 上限丢最旧 2 例） | local-dev/kg-import-e2e.mjs#KG-S42b1…b3（重导 done 且 新建0/合并2/新边0、图库 13 节点 16 边不增长） | 单测 PASS 2/2；E2E 待 Memgraph |
| @KG-S44 | `GraphSearchTextHelperTests`（预算内全保留/超预算丢尾段并标记/单段超限前缀截断 3 例）+ `GraphSearchServiceTests`（度数版邻接调用与邻居行语义回归） | kg-search-e2e 扩展（需 Memgraph，待补） | 单测 PASS 90/90 总量（2026-11-08）；E2E 待补 |
| @KG-S48 | `KgCypherReadOnlyGuardTests`（13 例：只读放行/写拒/多语句拒/字面量内写词不误伤/空与超长拒） | @manual（需 Memgraph+对话模型真实冒烟：生成→守卫→执行→高亮全链路） | 单测 PASS 13/13、总 103/103（2026-11-08）；冒烟待补 |
| @KG-S49 | —（规则校验由 Validator 覆盖：≤50 条/键 long/值≤100；类型归属与清除语义为 Handler 直查，Moq 桩待补） | **真实链路+视觉走查（Edge headless，2026-11-09）**：设置页样式卡渲染（产品/功能/会员版本三行）→ 真实类型 id 324 PUT `__type__` 200 → 详情回显 `{"324":"__type__"}` → null 清除 200；错误路径「不存在的实体类型：1」400 文案实测；**画布视觉验证**——配规则后重进画布，「云途网盘」节点 label 显示「云途网盘 · 产品」（截图实证）；期间修复 Kiota additionalData 包裹（Dictionary 形态归一化进 detail wrapper，同节点 properties 坑） | 后端链路+视觉 PASS（2026-11-09） |
| @KG-S44 | `GraphSearchTextHelperTests`（3 例）+ 服务层度数邻接（见上） | kg-search-e2e#KGS-S44a…c（邻居携 degree/neighborTopK=3 生效/越界 400，已预置待 Memgraph 执行） | 断言就绪（2026-11-09）；E2E 待 Memgraph |
| @KG-S45 | `UpdateKnowledgeGraphExtractModelCommandHandler`（校验：不存在/停用/非 conversation 400，null 清除；**单测待补**——GetTeamUsableModelIdsAsync 静态扩展需集成桩） | kg-import-e2e 扩展（回退断言：默认生效/请求优先/双空 400，待补） | dotnet build 0 error + openapi 契约确认（2026-11-08）；单测/E2E 待补 |
| @KG-S29 | —（JSON 解析器为容错手写，规则由 payload validator 把关；服务管线复用外部导入既有单测覆盖；疑似重复检测复用向量栈无独立单测，E2E 覆盖） | local-dev/kg-import-e2e.mjs#KG-S29a…m | **E2E PASS 20/20（2026-09-27，含 KG-S26 7 项）** |
| @KG-S31 | `ui/src/pages/teams/knowledgegraph/__tests__/TeamKnowledgeGraphs.test.tsx`（工具栏搜索：输入不触发不过滤/查询后命中过滤/无命中筛选空态） | —（纯前端行为） | vitest PASS 11/11（2026-11-07） |
| @KG-S35 | `ui/src/pages/knowledgegraph/KnowledgeGraphCanvas.tsx`（expandedBy 记账 + 收起回收规则）+ Detail 测试 G6 桩覆盖编排 | @manual（浏览器走查：双击展开标记/再双击回收） | vitest 795/795 编译级覆盖；走查待执行（2026-11-08） |
| @KG-S36 | 同上（图例过滤/搜索定位为画布内交互，jsdom 不渲染 G6） | @manual（浏览器走查：多选显隐不重载/搜索高亮居中/未命中转重载） | 走查待执行（2026-11-08） |
| @KG-S37 | `MergeKnowledgeGraphNodesCommandHandlerTests`（重挂计算+自环丢弃+向量增量 Verify / 约束 400 / ≥20 触边 409 与 confirm 放行 / 源缺失 404）+ `canvas/__tests__/MergeNodesModal.test.tsx`（默认目标=度数最高/Popconfirm 后 confirm:true 调用与 onMerged 回调/单候选禁用）+ ImportPage 测试「去合并」入口断言 | kg-e2e 扩展段已就绪（需 Memgraph，待执行） | 单测 PASS 4/4、vitest PASS 3/3（2026-11-08）；E2E 待图库 |
| @KG-S38 | `QueryKnowledgeGraphCanvasCommand.Validate`（depth 1~2）+ `CypherKnowledgeGraphStore.QueryCanvasAsync` depth 扩展 | kg-e2e 扩展（需 Memgraph，待补） | dotnet build 0 error；E2E 待补 |
| @KG-S39 | `KnowledgeGraphRecallTest.test.tsx`（结果区「在画布查看」渲染 + MemoryRouter） | —（跳转联动为前端行为） | vitest PASS 3/3（2026-11-08） |
| @KG-S40 | —（degree 由 Cypher `size((n)--())` 携带，真库断言） | kg-e2e 扩展（需 Memgraph，待补） | E2E 待补 |
| @KG-S32 | `QueryKnowledgeGraphsCommandHandler`（实时计数已退役：响应项不携带 entityCount/relationCount）+ `TeamKnowledgeGraphs.test.tsx`（卡片布局/徽标断言 13/13） | kg-e2e.mjs#KG-S47a（字段缺席断言）；数量改在详情/模型页按需统计（schema/内省既有链路） | vitest PASS 13/13（2026-11-08 退役改写） |
| @KG-S46 | `TestKnowledgeGraphConnectionCommandHandler`（**单测待补**——临时驱动需集成环境）；`ui/src/pages/settings/__tests__/Settings.test.tsx`（保存顺序/测试未通过不落开关/测试按钮 3 例） | kg-e2e.mjs#KG-S46a~f（ok=true/错误地址限幅 ok=false/非法 400×2/401/403）+ 图库宕机真机实测 6 路全过（2026-11-08） | vitest PASS 30/30；E2E 真机断言 PASS、脚本待 Memgraph 恢复后全量执行 |
| @KG-S47 | `QueryKnowledgeGraphsCommandHandler`（纯业务库查询） | kg-e2e.mjs#KG-S47a（items 无 entityCount/relationCount、enabled 回显）+ 图库宕机 44ms 实测 | PASS（2026-11-08 真机实测；脚本待 Memgraph 恢复） |
| @KG-S41 | local-dev/kg-import-e2e.mjs#KG-S41a…e（`.py` 专用端点上传落 public/chat/、`.exe` 400、对话附件通道 .py 仍 400、导入任务 done） | 提取层复用知识库 D47（EPUB/ODT/RTF/纯文本兜底已在 wiki-e2e WK-15 全链路验证） | 端点与白名单段已 curl 实测（2026-11-08，5000 实例：py 200/public-chat/、exe 400、chat 400）；E2E 全量段需 Memgraph（本机 docker 不可用），待图库环境回归 KG-S26 全脚本 |

## 外部开放接口映射（KX-*，/api/teamres/knowledge-graph）

> 编号沿用证据脚本 `kg-external-e2e.mjs`（不复用 KG-\*）；行为场景见 [bdd 外部接口段](./bdd.md#feature-外部开放接口应用-token-kx)。

| 场景（脚本编号） | 覆盖 | 结果 |
|---|---|---|
| KX-01 | 应用 token 换取；列表仅本团队 managed 图谱 | **PASS 58/58（2026-09-15）** |
| KX-02 | 跨团队/不存在一律 404 | 同上 |
| KX-03 | 实体类型/关系类型 CRUD、被引用删除 409 | 同上 |
| KX-04 | 节点 CRUD/分页/邻接、schema 计数、DETACH 级联 | 同上 |
| KX-05 | 边 CRUD/分页、起止约束 400 | 同上 |
| KX-06 | 批量导入 ≤200、任一失败整批 400 且数据不变 | 同上 |
| KX-07 | connected 图谱外部写 409 | 同上 |
| KX-08 | 无/伪造/内部 token 401·403 | 同上 |
| KX-09 | 全量凭证读写（kg 分档已退役改写）：不传/传 scopes 均 200、全量 token 与 moai-ac- 直连读写均放行、跨资源组 wiki/list 放行 | **PASS 110/110（2026-11-04）** |
| KX-10 | 外部批量导入：autoCreateTypes + 业务 key + 名称引用 + 逐条失败报告 + by-key 查询 | **PASS 101/101（2026-09-27）** |
| KX-11 | 幂等重导：key upsert 更新/边去重跳过/整体覆盖语义/key 收养/全量 key 导入与批删放行（原只读门禁 403 断言随 scope 退役改写）/按 key 批删 | 同上 |
| KX-15 | 图资源 key（moai-tr-）包围圈：生成 moai-tr- key/本图 REST 放行/团队级 KG list 仅含绑定图/越 wiki 与越圈其它图 403 insufficient_scope | **PASS 110/110（2026-11-04）** |
| KX-12 | connected 图谱导入 409（图库不可达时跳过） | 同上 |
| KX-13 | 按 key 同步闭环：单节点 key 创建/收养、keys/list 分页枚举、edges/batch-delete 按引用删边（幂等/幽灵 key 行级失败/先删边后删点） | 同上 |
| KX-14 | 导入预检 validateOnly：预测计数与逐条结果/id 恒空/不建类型不落库/同体真导入对照 | 同上 |

## 知识图谱 MCP 映射（KGM-*，/api/teamres/knowledge-graph/{kgId}/mcp）

| 场景（脚本编号） | 覆盖 | 结果 |
|---|---|---|
| KGM-S1 | 鉴权门禁：401/伪造 key 401/moai-ac- 全量 key 握手（kg_mcp 概念已退役）/wiki 资源 key 越域 403 insufficient_scope/kgId 404·跨团队 404 | **PASS 36/36（2026-11-04）** |
| KGM-S8 | 绑定图 moai-tr- 资源 key initialize 200 | **PASS 36/36（2026-11-04）** |
| KGM-S2 | 工具域隔离：KG 端点恰好四工具/wiki 端点仍三工具/跨域工具调用拒绝 | 同上 |
| KGM-S3 | list_knowledge_graphs 团队托管列表 | 同上 |
| KGM-S4 | schema：默认路径 kgId/属性定义/关系约束/跨团队拒绝 | 同上 |
| KGM-S5 | 节点搜索：关键字/类型名/类型过滤/分页/跨团队拒绝 | 同上 |
| KGM-S6 | 向量召回：参数校验/未配向量化 skippedHint/命中带邻居/阈值/top | 同上 |
| KGM-S7 | 协议：未知工具/GET 405/无状态直调/通知 202/REST 回归 | 同上 |

## Text2Cypher 查图插件映射（KT-S*，`/api/team/{teamId}/plugin/dynamic`）

> 编号沿用证据脚本 [kg-text2cypher-e2e.mjs](../../local-dev/kg-text2cypher-e2e.mjs)（不复用 KG-\*/KX-\*）；行为场景见 [bdd Text2Cypher 段](./bdd.md#feature-text2cypher-查图插件消费kt-s)。单测挂 [tests/MoAI.AIPlugin.Dynamic.Tests/](../../tests/MoAI.AIPlugin.Dynamic.Tests/)（插件工程，非 KG 测试工程）；插件本体与安全设计见 [Text2Cypher 设计文档](../superpowers/specs/2026-09-21-kg-text2cypher-plugin-design.md)。

| 场景 | 覆盖 | 结果 |
|---|---|---|
| @KT-S1 | —（模板注册走 `PluginRegistry` 自动扫描，由 `dynamic-plugin-e2e.mjs#DYN-S48` 断言）；实例创建由 teamplugin 保存 Handler 校验归属 | 单测 **PASS 36/36（2026-09-22）**；**E2E PASS 15/15（2026-09-22）** |
| @KT-S2 | teamplugin `SaveTeamDynamicPluginCommandHandler` 图谱存在性与团队归属校验（越团队 403/图谱不存在 404） | **E2E PASS 15/15（2026-09-22）** |
| @KT-S3 | `KgCypherAccessService` schema 摘要（托管图查 PG 类型表 + 图库采样；接入图走内省缓存） | **E2E PASS 15/15（2026-09-22）** |
| @KT-S4 | `KgCypherAccessService` 托管图按 $kgId 执行 + 表格化 | **E2E PASS 15/15（2026-09-22）** |
| @KT-S5 | `KgCypherAccessService` $kgId 门禁（托管图缺失报教学式错误）+ 结果侧 kgId 归属校验 | **E2E PASS 15/15（2026-09-22）** |
| @KT-S6 | `CypherReadOnlyGuardTests`（黑名单 10 关键字逐个、注释/字面量剥除后扫描、大小写、多语句、非只读首关键字等 29 例） | 单测 **PASS 36/36（2026-09-22）**；**E2E PASS 15/15（2026-09-22）** |
| @KT-S7 | `KgCypherAccessService` 行数截断（仿 `SqlResultReader`，超 `maxRows` 置 truncated） | **E2E PASS 15/15（2026-09-22）** |
| @KT-S8 | —（依赖慢查询负载，@manual） | 人工验证 |
| @KT-S9 | `KgCypherAccessService` 接入图按库路由（免 $kgId） | **E2E PASS 15/15（2026-09-22）** |
| @KT-S10 | 团队插件运行门禁（跨团队实例 key 404） | **E2E PASS 15/15（2026-09-22）** |

> 附带：`local-dev/dynamic-plugin-e2e.mjs#DYN-S48` 断言 `kg_cypher_query` 出现在动态模板注册表（不依赖图数据库，随 DYN 套件回归）。

## 图检索消费层映射（KGS-S*，`POST /api/knowledge-graph/{id}/search` 等）

> 编号沿用证据脚本 [kg-search-e2e.mjs](../../local-dev/kg-search-e2e.mjs)（不复用 KG-\*/KX-\*/KT-\*）；行为场景见 [bdd 图检索消费层段](./bdd.md#feature-图检索消费层kgs-s)。单测分布：检索/向量化挂 [tests/MoAI.KnowledgeGraph.Tests/](../../tests/MoAI.KnowledgeGraph.Tests/)，对话工具挂 [tests/MoAI.AI.Core.Tests/GraphAppToolProviderTests.cs](../../tests/MoAI.AI.Core.Tests/GraphAppToolProviderTests.cs)，绑定挂 [tests/MoAI.App.Tests/](../../tests/MoAI.App.Tests/SaveAppAgentConfigCommandHandlerTests.cs)，工作流节点挂 [tests/MoAI.App.Workflow.Tests/](../../tests/MoAI.App.Workflow.Tests/)。

| 场景 | 验证物（单测） | E2E | 结果（日期） |
|---|---|---|---|
| @KGS-S1 | `UpdateKnowledgeGraphEmbeddingConfigCommandHandlerTests`（合法/已授权非公开保存 2 例 + ModelKind 不符 400） | kg-search-e2e.mjs#KGS-S1a…c（S1c 详情回读为条件式断言） | 单测 PASS；**E2E PASS 40/40（2026-09-22）** |
| @KGS-S2 | `GraphSearchServiceTests`（跨图按分合并与总量钳制、邻居方向/关系名/类型名、非法入参短路） | kg-search-e2e.mjs#KGS-S2a…k | 单测 PASS；**E2E PASS** |
| @KGS-S3 | `KgEmbeddingServiceTests.ProcessDeltaAsync_UpsertNode_ReplacesWithExpectedRecord`（替换式 upsert） | kg-search-e2e.mjs#KGS-S3a/b | 单测 PASS；**E2E PASS** |
| @KGS-S4 | `KgEmbeddingServiceTests.ProcessDeltaAsync_DeleteNode_CallsDeleteNodeVectors` | kg-search-e2e.mjs#KGS-S4a…c | 单测 PASS；**E2E PASS** |
| @KGS-S5 | `GraphSearchServiceTests.SearchAsync_MinScore_FiltersLowAndNullScores` | kg-search-e2e.mjs#KGS-S5（数据驱动阈值：minScore=min(0.999, maxScore+0.0005)，桩分布不可分时 INFO 跳过） | 单测 PASS；**E2E PASS** |
| @KGS-S6 | `GraphSearchServiceTests.SearchAsync_WhenGraphNotConfigured_SkipsWithHintAndSearchesOtherGraphs`（工具/服务路径跳过提示；检索 API 409 分支由 E2E 直断） | kg-search-e2e.mjs#KGS-S6a/b | 单测 PASS；**E2E PASS** |
| @KGS-S7 | `UpdateKnowledgeGraphEmbeddingConfigCommandHandlerTests.Handle_ModelNotAuthorized_Throws400`（不存在/未授权 400） | kg-search-e2e.mjs#KGS-S7a…c | 单测 PASS；**E2E PASS** |
| @KGS-S8 | `SaveAppAgentConfigCommandHandlerTests`（GraphIds：他团队 400、接入图 400、本团队托管图保存回读、空绑定落 []） | kg-search-e2e.mjs#KGS-S8a…g（S8g 回读为条件式断言） | 单测 PASS；**E2E PASS** |
| @KGS-S9 | `KnowledgeGraphSearchNodeTests`（静态 graphId、输出结构、空命中、text 截断、缺 query/graphId 失败、topK 钳制）+ `GraphSearchTextHelperTests`（片段构造/截断 8 例） | kg-search-e2e.mjs#KGS-S9a…f | 单测 PASS；**E2E PASS** |
| @KGS-S10 | —（召回测试复用 GraphSearchService 与召回页面表单，E2E 覆盖） | local-dev/kg-search-e2e.mjs#KGS-S10a…f | **E2E PASS 46/46（2026-09-27）** |

> 附带覆盖（未单列场景）：`GraphAppToolProviderTests` 7 例——绑定空不注册工具、检索回传、缺 query 失败、topK 钳制（越界/缺省/超大）与 16KB payload 预算截断（`hitsTruncated`）；`DeleteKnowledgeGraphCommandHandlerTests` 3 例——删托管图清理向量集合（软删前）；`QueryKnowledgeGraphSearchCommandHandler` 接入图 409 分支由 E2E 直断。

## v2 前端映射（vitest）

| 验证物 | 覆盖 | 结果 |
|---|---|---|
| `ui/src/pages/knowledgegraph/__tests__/KnowledgeGraphDetail.test.tsx` | 托管图菜单 图览/维护/设置（三段）、接入图 图览/模型/设置、默认图览（调 canvas 接口）、接入图只读徽标、旧路径重定向维护页 | PASS 5/5（2026-09-27） |
| `ui/src/pages/teams/knowledgegraph/__tests__/TeamKnowledgeGraphs.test.tsx` | 团队图谱卡片墙、新建/接入弹窗、点击卡片默认进入图览 | PASS（2026-09-21） |
| `ui/src/pages/knowledgegraph/__tests__/KnowledgeGraphEntities.test.tsx` / `KnowledgeGraphRelations.test.tsx` | Admin 可写、Member 只读（写入口不渲染）、能力未开启不渲染 | PASS（2026-09-14） |
| `ui/src/pages/settings/__tests__/Settings.test.tsx` | KG_* 设置键、图数据库类型保存、关闭时不提交连接信息、卡片折叠/展开 | PASS 5/5（2026-09-17） |

## 备注

- `Handle_WhenDisabled_Throws409` / `Handle_WhenEnabledButUriEmpty_Throws409` / `CreateEntityType_WhenSettingsDisabled_Throws409` 覆盖能力门禁（对应 SDD §6，未单列场景编号）。
- `AuthorizeManagedAsync` 的只读分支同时约束 schema 与节点 / 边写 Handler，@KG-S12 两个 E2E 检查分别验证节点与实体类型写被拒。
- E2E 执行结果统一记入 [sop.md 验收记录](./sop.md#验收记录)。
