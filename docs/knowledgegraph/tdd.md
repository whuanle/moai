# 知识图谱模块验证映射（TDD）

> 关联：[SDD](./sdd.md) ｜ [BDD](./bdd.md) ｜ [TDD](./tdd.md) ｜ [SOP](./sop.md) ｜ 证据：[local-dev/kg-e2e.mjs](../../local-dev/kg-e2e.mjs)、[local-dev/kg-text2cypher-e2e.mjs](../../local-dev/kg-text2cypher-e2e.mjs)、[local-dev/kg-search-e2e.mjs](../../local-dev/kg-search-e2e.mjs) ｜ 单测：[tests/MoAI.KnowledgeGraph.Tests/](../../tests/MoAI.KnowledgeGraph.Tests/)、[tests/MoAI.AIPlugin.Dynamic.Tests/](../../tests/MoAI.AIPlugin.Dynamic.Tests/)

## 自检记录

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
- 2026-09-27 导入页下载示例 + MCP 菜单：JSON 导入表单加「下载示例 JSON」（Blob 下载可导入的完整示例，含 key/类型名引用/属性/边两种端点引用形态）；详情页新增「MCP」菜单（managed only，镜像 wiki WikiMcp：serverinfo serviceUrl + /api/external/knowledge-graph/{kgId}/mcp 地址框复制 + 四只读工具清单 + kg_mcp 鉴权提示，`KnowledgeGraphMcpPage`）；KG vitest **26/26**（Detail MCP 菜单断言 managed 有/connected 无 + ImportPage 下载示例 1 例）、typecheck/lint 0（Canvas WIP 除外）。
- 2026-09-27 导入入口收敛：移除维护页「录入实例」步骤的 AI 导入按钮与 `KnowledgeGraphImportModal`（组件删除、i18n import.title/button/cancel/close 键清理 zh/en），AI 导入统一走「导入」菜单页内嵌表单；KG vitest 25/25、typecheck/lint 0（Canvas WIP 除外）。
- 2026-09-27 导入页（新增「导入」菜单 + /import-json）：内部 `POST {id}/import-json`（仅托管图 Admin+）容错解析导入页 JSON 文本（AllowTrailingCommas+注释跳过，解析错误带行定位）→ 载荷校验（KnowledgeGraphImportPayloadValidator，与外部 /import 共用规则集；页面控件 mode/autoCreateTypes/validateOnly 覆盖 JSON 内同名字段）→ **KnowledgeGraphDataImportService 共享管线**（外部 /import handler 同步瘦身为委托，外部入口行为零变化）。前端新增导入菜单页（Tabs：AI 智能导入内嵌表单 + JSON 导入粘贴/上传/预检/逐条结果表），syncapi 重生成。E2E kg-import-e2e 扩 @KG-S29a~i **16/16**；回归 kg-external **101/101**；前端 KG 目录 vitest **25/25**（新增 ImportPage 3 例 + Detail 导入菜单断言）、typecheck/lint 0（Canvas WIP 除外）。
- 2026-09-27 知识图谱 MCP 服务器（@KGM-S1~S7）：端点 `/api/external/knowledge-graph/{kgId}/mcp`（streamable HTTP 无状态），新增 `kg_mcp` scope 位（256，接入/团队 key 均可勾选；asserts/access_app_kg_mcp_scopes.sql 存量 `|=256`、DEFAULT 486，开发库 219 条已回填）；四只读工具 list_knowledge_graphs/get_knowledge_graph_schema/search_knowledge_graph_nodes/search_knowledge_graph_recall（召回复用 GraphSearchService，未配向量化返回 skippedHint 不报错、接入图 409）。与知识库 MCP 共享 McpServerOptions——**工具域隔离**经 `KnowledgeGraphMcpToolGate`（AddListToolsFilter/AddCallToolFilter 按请求路径过滤；SDK spike 确认 filter 为中间件形态 `next => async (RequestContext<TParams>, ct)`、工具名在 `Params.Name`；未知工具放行 SDK 原生 -32602，避免破坏 WM-S6a 基线）；中间件 GetRequiredResourceScope 对 `/mcp` 短路（MCP 端点 403 insufficient_scope 曾因落入 kg_write 分档）。E2E `kg-mcp-e2e.mjs` **35/35 零 SKIP**（本地桩 embedding）；回归 wiki-mcp 30/30、kg-external 101/101、team-apikey-scope 49/49。
- 2026-09-27 维护步骤条去重序号（前端）：antd Steps 自带圆圈序号，i18n guide.step1~3 文案里的手写 ①②③（en 为 "1. "）与之重复——去掉前缀；无引用的 guide.step4 键删除（zh/en）；Detail 测试断言同步。vitest KG 22/22。
- 2026-09-27 维护页工具栏合并（前端）：录入实例/连接关系两步骤的筛选栏原在 DataTable 外部、刷新按钮在 DataTable 内置 toolbar 行，导致刷新独占一行——筛选栏（新建/AI 导入/类型筛选/搜索/节点 Tag）整体迁入 `DataTable.toolbar` prop 与刷新同行；vitest KG 22/22、eslint 0 error、typecheck（除 Canvas 并行 WIP）0。
- 2026-09-27 类型弹窗美化（前端）：实体/关系类型编辑弹窗加宽 560、颜色 `Input[type=color]` 换 antd `ColorPicker`（showText/allowClear，`getValueFromEvent` 归一 hex 字符串兼容后端可空 color 契约）、名称+颜色同行、关系起止类型并排、描述 TextArea autoSize、属性行改卡片式两行布局（名称 flex 加宽/说明独占一行，底色取 `token.colorFillQuaternary`）。vitest KG 目录 22/22、改动文件 eslint 0 error（4 warning 为存量非组件导出提示）、typecheck 除并行画布 WIP 外 0。
- 2026-09-27 维护页「模型」步骤微调（前端）：去掉「模型是什么？」说明 Alert（i18n schema.introTitle/introDesc zh/en 键删除），实体类型/关系类型区块标题加数量 Tag——关系类型维护区（新增/编辑/删除）本就在实体类型表格下方，去掉 Alert 后首屏直达。vitest KG 目录 22/22（Schema/Detail/Entities/Relations/Maintenance）、改动文件 eslint 0（typecheck 唯一错误在 KnowledgeGraphCanvas.tsx:733，系并行画布 WIP 与本改动无关）。
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
| @KG-S1 | `CreateKnowledgeGraphCommandHandlerTests.Handle_WhenEnabledAndAdmin_CreatesGraph`（mode=managed、database=null） | kg-e2e.mjs#KG-S1a/b | 单测 PASS；E2E 未执行（2026-09-10） |
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
| @KG-S25 | —（G6 画布交互无 jsdom 渲染，浏览器走查覆盖：拖动/节点详情/边详情/边标签/两段式建边） | @manual（走查记录见 sop §5） | PASS（2026-09-21 浏览器实测） |
| @KG-S27 | `ui/src/pages/knowledgegraph/__tests__/KnowledgeGraphDetail.test.tsx`（画布 limit 默认 200 调用）+ `KnowledgeGraphCanvas.tsx` 实现（React Flow + d3-force，jsdom 直接渲染无需 mock） | @manual（浏览器走查：图览无步骤条/导入按钮、画布撑满、缩放/适应/全屏、截断提示） | PASS（2026-09-27 浏览器实测 Edge，React Flow 版） |
| @KG-S28 | `ui/src/pages/knowledgegraph/__tests__/KnowledgeGraphDetail.test.tsx`（维护步骤条渲染、?step= 路由、旧路径重定向后「新建实例」入口） | —（纯前端行为） | vitest PASS（2026-09-27） |
| @KG-S26 | `KnowledgeGraphImportParserTests`（JSON/fence/snake_case/缺字段/非文本 4 例） | local-dev/kg-import-e2e.mjs#KG-S26a…g（桩模型） | 单测 PASS；**E2E PASS 7/7（2026-09-21）**；真实模型冒烟（Qwen3.5 9B 导入 txt → 6 节点 6 边入图） |
| @KG-S29 | —（JSON 解析器为容错手写，规则由 payload validator 把关；服务管线复用外部导入既有单测覆盖；疑似重复检测复用向量栈无独立单测，E2E 覆盖） | local-dev/kg-import-e2e.mjs#KG-S29a…m | **E2E PASS 20/20（2026-09-27，含 KG-S26 7 项）** |

## 外部开放接口映射（KX-*，/api/external/knowledge-graph）

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
| KX-09 | kg_read/kg_write 分档：只读 token/直连读写分档/跨资源组隔离/补权刷新放行 | **PASS 67/67（2026-09-26）** |
| KX-10 | 外部批量导入：autoCreateTypes + 业务 key + 名称引用 + 逐条失败报告 + by-key 查询 | **PASS 101/101（2026-09-27）** |
| KX-11 | 幂等重导：key upsert 更新/边去重跳过/整体覆盖语义/key 收养/只读门禁 403/按 key 批删 | 同上 |
| KX-12 | connected 图谱导入 409（图库不可达时跳过） | 同上 |
| KX-13 | 按 key 同步闭环：单节点 key 创建/收养、keys/list 分页枚举、edges/batch-delete 按引用删边（幂等/幽灵 key 行级失败/先删边后删点） | 同上 |
| KX-14 | 导入预检 validateOnly：预测计数与逐条结果/id 恒空/不建类型不落库/同体真导入对照 | 同上 |

## 知识图谱 MCP 映射（KGM-*，/api/external/knowledge-graph/{kgId}/mcp）

| 场景（脚本编号） | 覆盖 | 结果 |
|---|---|---|
| KGM-S1 | 鉴权门禁：401/401/接入与团队 key 握手/无 kg_mcp 403/kgId 404·跨团队 404 | **PASS 35/35（2026-09-27）** |
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
