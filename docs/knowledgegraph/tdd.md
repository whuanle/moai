# 知识图谱模块验证映射（TDD）

> 关联：[SDD](./sdd.md) ｜ [BDD](./bdd.md) ｜ [TDD](./tdd.md) ｜ [SOP](./sop.md) ｜ 证据：[local-dev/kg-e2e.mjs](../../local-dev/kg-e2e.mjs)、[local-dev/kg-text2cypher-e2e.mjs](../../local-dev/kg-text2cypher-e2e.mjs) ｜ 单测：[tests/MoAI.KnowledgeGraph.Tests/](../../tests/MoAI.KnowledgeGraph.Tests/)、[tests/MoAI.AIPlugin.Dynamic.Tests/](../../tests/MoAI.AIPlugin.Dynamic.Tests/)

## 自检记录

- 构建：`dotnet build src/MoAI/MoAI.csproj` → **0 错误**（2026-09-15，v2.1）
- 单测：`dotnet test tests/MoAI.KnowledgeGraph.Tests/MoAI.KnowledgeGraph.Tests.csproj` → **PASS 40/40**（2026-09-15，v2.1 新增接入图 refresh 透传与 changes 映射；存量覆盖模板映射、探活 / 内省、能力门禁、角色判定、只读拦截、类型引用拦截、分页钳制、删图清库分支）
- E2E：`node local-dev/kg-e2e.mjs http://127.0.0.1:5000` → **PASS 47/47**（2026-09-15，v2.1 新增 S17 接入图画布（entityLabel/relationName/label 过滤）、S18 接入图邻接（elementId、404）、S19 内省缓存/强制刷新）。脚本无图数据库时打印 `SKIP` 并退出码 0（CI 友好）。
- 前端：`npm run typecheck` 0 错误；`npm run lint` 0 错误（3 个 skills 既有警告）；`npm run test` **258/258**（2026-09-15，syncapi 重生成后全量通过）。
- v2.2 头像：单测 **42/42**（新增 `UpdateKnowledgeGraphAvatarCommandHandlerTests` 2 例）；E2E `node local-dev/kg-e2e.mjs http://127.0.0.1:5020` → **PASS 52/52**（2026-09-15，新增 S20：真实存储直传→设置→详情/列表回显→未登记 404）。
- v2.3 模型属性：单测 **43/43**（新增 `QuerySchema_ReturnsEntityTypeProperties`）；E2E → **PASS 59/59**（2026-09-15，新增 S21 属性全链路 7 项；顺带修复 v1 遗留缺陷：Update 节点/边/实体类型/关系类型 4 个命令 validator 校验路由字段导致所有编辑操作 400，移除路由字段规则并新增 S21g 编辑覆盖断言）。
- 外部开放接口：`node local-dev/kg-external-e2e.mjs http://127.0.0.1:5210` → **PASS 58/58**（2026-09-15，KX-01~KX-08：应用 token 换取、团队级列表、跨团队 404、类型/节点/边 CRUD 与分页邻接、批量整批拒绝、connected 409、仅应用 token）；无图数据库或 `KG_ENABLED=false` 时打印 SKIP 并退出码 0。
- v2.4 设置页体验：vitest **303/303**（Settings 5/5：卡片折叠/展开、图数据库类型保存、关闭不提交连接信息）；typecheck 0、lint 0 错误（2026-09-17）。
- v2.5 物流模板 + 默认图览：单测 **43/43**（模板断言改物流运输 + 预置属性落库断言；顺带修复脚手架重生成后测试种子缺 `Properties` 非空赋值的 7 例存量失败）；E2E `node local-dev/kg-e2e.mjs http://127.0.0.1:5310` → **PASS 61/61**（2026-09-21，5310 独立实例，KG-S2 扩为 a…h 含航段预置属性断言，图库可达零 SKIP）；前端 typecheck 0、lint 0 错误（8 warning 存量）、vitest **424/424**。
- v2.6 建图弹窗上传头像：前端 typecheck 0、lint 0 错误（8 warning 存量）、vitest **429/429**（TeamKnowledgeGraphs 扩至 10 例：选图创建登记/登记失败不阻断/未选不调用）。零后端改动——创建成功后前端串联既有 `POST {id}/avatar` 登记（KG-S20 链路），无需 syncapi。
- v2.7 模板一次性预置示例实例与关系：单测 **45/45**（新增 `Handle_WithTemplate_SeedsExampleNodesAndEdges` 11 节点/15 边与端点映射断言、`Handle_WithTemplate_WhenSeedingFails_PurgesGraphAndRollsBack` 失败清理回滚）；E2E `node local-dev/kg-e2e.mjs http://127.0.0.1:5000` → **PASS 65/65**（2026-09-21，KG-S2 扩至 a…k：13 节点/16 边总数、示例航段属性、建图即得完整图览，两跑一致）。
- v2.8 图览交互（拖动/详情/关系标签）：前端 typecheck 0、lint 0 错误（8 warning 存量）、vitest **429/429**；浏览器实测（走查团队物流图 105，G6 真渲染）：节点拖动保持、节点详情抽屉（类型/属性）、边详情抽屉（关系名/起止）、边关系名标签、两段式点击建边弹窗起止回显全过。顺带修复两处前端缺陷：①G6 v5 点击判定误用 `target.type`（恒 undefined）致点击节点展开邻接自 v2 起实际失效，改 `targetType`；②Kiota 把节点 `properties` 字典收进 `additionalData` 致属性读取恒空（实例列表同受影响），`flattenNodeProperties` 归一化。
- v2.9 AI 导入文件生成图谱：单测 **49/49**（新增 `KnowledgeGraphImportParserTests` 4 例）；E2E `node local-dev/kg-import-e2e.mjs http://127.0.0.1:5000` → **PASS 7/7**（桩模型：模板图建图、model-options、非法 objectKey 400、导入 2 节点 1 边入图、画布计数、航段属性、空白模板图 409）；真实模型冒烟（Qwen3.5 9B 导入 txt：新增 6 节点 6 边，画布 11→17，航段属性完整）；前端 typecheck 0、lint 0 错误、vitest **430/430**；浏览器走查导入弹窗与结果展示。踩坑：`string.Equals(x, y, StringComparison)` 不可翻译为 SQL（model-options 首版放查询内 500，改为内存过滤）；GLM/DeepSeek 渠道对 `DisableThinking`（reasoning_effort=none）报 400001/402 为渠道侧问题，导入对模型无思考链要求时建议选 Qwen 系（2026-09-21）。
- Text2Cypher 查图插件（KT）：单测 `dotnet test tests/MoAI.AIPlugin.Dynamic.Tests/MoAI.AIPlugin.Dynamic.Tests.csproj` → **PASS 36/36**（2026-09-22，`CypherReadOnlyGuardTests` 29 例 + `KgCypherQueryPluginParamsTests` 7 例）；E2E `node local-dev/kg-text2cypher-e2e.mjs` **已就绪待运行**（后端待重启加载含 `kg_cypher_query` 的新构建，运行前勿对旧构建执行）。

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
| @KG-S23 | `ui/src/pages/knowledgegraph/__tests__/KnowledgeGraphDetail.test.tsx`、`ui/src/pages/teams/knowledgegraph/__tests__/TeamKnowledgeGraphs.test.tsx`（默认图览 + 画布接口调用） | —（纯前端行为） | vitest PASS（2026-09-21） |
| @KG-S24 | `ui/src/pages/teams/knowledgegraph/__tests__/TeamKnowledgeGraphs.test.tsx`（创建选图→建图→登记、登记失败不阻断、未选不调用） | —（创建弹窗前端编排，复用 KG-S20 直传登记链路） | vitest PASS 3/3（2026-09-21） |
| @KG-S25 | —（G6 画布交互无 jsdom 渲染，浏览器走查覆盖：拖动/节点详情/边详情/边标签/两段式建边） | @manual（走查记录见 sop §5） | PASS（2026-09-21 浏览器实测） |
| @KG-S26 | `KnowledgeGraphImportParserTests`（JSON/fence/snake_case/缺字段/非文本 4 例） | local-dev/kg-import-e2e.mjs#KG-S26a…g（桩模型） | 单测 PASS；**E2E PASS 7/7（2026-09-21）**；真实模型冒烟（Qwen3.5 9B 导入 txt → 6 节点 6 边入图） |

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

## Text2Cypher 查图插件映射（KT-S*，`/api/team/{teamId}/plugin/dynamic`）

> 编号沿用证据脚本 [kg-text2cypher-e2e.mjs](../../local-dev/kg-text2cypher-e2e.mjs)（不复用 KG-\*/KX-\*）；行为场景见 [bdd Text2Cypher 段](./bdd.md#feature-text2cypher-查图插件消费kt-s)。单测挂 [tests/MoAI.AIPlugin.Dynamic.Tests/](../../tests/MoAI.AIPlugin.Dynamic.Tests/)（插件工程，非 KG 测试工程）；插件本体与安全设计见 [Text2Cypher 设计文档](../superpowers/specs/2026-09-21-kg-text2cypher-plugin-design.md)。

| 场景 | 覆盖 | 结果 |
|---|---|---|
| @KT-S1 | —（模板注册走 `PluginRegistry` 自动扫描，由 `dynamic-plugin-e2e.mjs#DYN-S48` 断言）；实例创建由 teamplugin 保存 Handler 校验归属 | 单测 **PASS 36/36（2026-09-22）**；E2E 已就绪待运行 |
| @KT-S2 | teamplugin `SaveTeamDynamicPluginCommandHandler` 图谱存在性与团队归属校验（越团队 403/图谱不存在 404） | E2E 已就绪待运行 |
| @KT-S3 | `KgCypherAccessService` schema 摘要（托管图查 PG 类型表 + 图库采样；接入图走内省缓存） | E2E 已就绪待运行 |
| @KT-S4 | `KgCypherAccessService` 托管图按 $kgId 执行 + 表格化 | E2E 已就绪待运行 |
| @KT-S5 | `KgCypherAccessService` $kgId 门禁（托管图缺失报教学式错误）+ 结果侧 kgId 归属校验 | E2E 已就绪待运行 |
| @KT-S6 | `CypherReadOnlyGuardTests`（黑名单 10 关键字逐个、注释/字面量剥除后扫描、大小写、多语句、非只读首关键字等 29 例） | 单测 **PASS 36/36（2026-09-22）**；E2E 已就绪待运行 |
| @KT-S7 | `KgCypherAccessService` 行数截断（仿 `SqlResultReader`，超 `maxRows` 置 truncated） | E2E 已就绪待运行 |
| @KT-S8 | —（依赖慢查询负载，@manual） | 人工验证 |
| @KT-S9 | `KgCypherAccessService` 接入图按库路由（免 $kgId） | E2E 已就绪待运行 |
| @KT-S10 | 团队插件运行门禁（跨团队实例 key 404） | E2E 已就绪待运行 |

> 附带：`local-dev/dynamic-plugin-e2e.mjs#DYN-S48` 断言 `kg_cypher_query` 出现在动态模板注册表（不依赖图数据库，随 DYN 套件回归）。

## v2 前端映射（vitest）

| 验证物 | 覆盖 | 结果 |
|---|---|---|
| `ui/src/pages/knowledgegraph/__tests__/KnowledgeGraphDetail.test.tsx` | 五段菜单、托管/接入图默认图览（调 canvas 接口）、接入图只读徽标 | PASS（2026-09-21） |
| `ui/src/pages/teams/knowledgegraph/__tests__/TeamKnowledgeGraphs.test.tsx` | 团队图谱卡片墙、新建/接入弹窗、点击卡片默认进入图览 | PASS（2026-09-21） |
| `ui/src/pages/knowledgegraph/__tests__/KnowledgeGraphEntities.test.tsx` / `KnowledgeGraphRelations.test.tsx` | Admin 可写、Member 只读（写入口不渲染）、能力未开启不渲染 | PASS（2026-09-14） |
| `ui/src/pages/settings/__tests__/Settings.test.tsx` | KG_* 设置键、图数据库类型保存、关闭时不提交连接信息、卡片折叠/展开 | PASS 5/5（2026-09-17） |

## 备注

- `Handle_WhenDisabled_Throws409` / `Handle_WhenEnabledButUriEmpty_Throws409` / `CreateEntityType_WhenSettingsDisabled_Throws409` 覆盖能力门禁（对应 SDD §6，未单列场景编号）。
- `AuthorizeManagedAsync` 的只读分支同时约束 schema 与节点 / 边写 Handler，@KG-S12 两个 E2E 检查分别验证节点与实体类型写被拒。
- E2E 待具备 Neo4j 环境后在 [sop.md](./sop.md#4-验收流程) 记录执行结果。
