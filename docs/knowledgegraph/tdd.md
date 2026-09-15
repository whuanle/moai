# 知识图谱模块验证映射（TDD）

> 关联：[SDD](./sdd.md) ｜ [BDD](./bdd.md) ｜ [TDD](./tdd.md) ｜ [SOP](./sop.md) ｜ 证据：[local-dev/kg-e2e.mjs](../../local-dev/kg-e2e.mjs) ｜ 单测：[tests/MoAI.KnowledgeGraph.Tests/](../../tests/MoAI.KnowledgeGraph.Tests/)

## 自检记录

- 构建：`dotnet build src/MoAI/MoAI.csproj` → **0 错误**（2026-09-15，v2.1）
- 单测：`dotnet test tests/MoAI.KnowledgeGraph.Tests/MoAI.KnowledgeGraph.Tests.csproj` → **PASS 40/40**（2026-09-15，v2.1 新增接入图 refresh 透传与 changes 映射；存量覆盖模板映射、探活 / 内省、能力门禁、角色判定、只读拦截、类型引用拦截、分页钳制、删图清库分支）
- E2E：`node local-dev/kg-e2e.mjs http://127.0.0.1:5000` → **PASS 47/47**（2026-09-15，v2.1 新增 S17 接入图画布（entityLabel/relationName/label 过滤）、S18 接入图邻接（elementId、404）、S19 内省缓存/强制刷新）。脚本无图数据库时打印 `SKIP` 并退出码 0（CI 友好）。
- 前端：`npm run typecheck` 0 错误；`npm run lint` 0 错误（3 个 skills 既有警告）；`npm run test` **258/258**（2026-09-15，syncapi 重生成后全量通过）。
- v2.2 头像：单测 **42/42**（新增 `UpdateKnowledgeGraphAvatarCommandHandlerTests` 2 例）；E2E `node local-dev/kg-e2e.mjs http://127.0.0.1:5020` → **PASS 52/52**（2026-09-15，新增 S20：真实存储直传→设置→详情/列表回显→未登记 404）。
- v2.3 模型属性：单测 **43/43**（新增 `QuerySchema_ReturnsEntityTypeProperties`）；E2E → **PASS 59/59**（2026-09-15，新增 S21 属性全链路 7 项；顺带修复 v1 遗留缺陷：Update 节点/边/实体类型/关系类型 4 个命令 validator 校验路由字段导致所有编辑操作 400，移除路由字段规则并新增 S21g 编辑覆盖断言）。
- 外部开放接口：`node local-dev/kg-external-e2e.mjs http://127.0.0.1:5210` → **PASS 58/58**（2026-09-15，KX-01~KX-08：应用 token 换取、团队级列表、跨团队 404、类型/节点/边 CRUD 与分页邻接、批量整批拒绝、connected 409、仅应用 token）；无图数据库或 `KG_ENABLED=false` 时打印 SKIP 并退出码 0。

## 映射表

| 场景 | 验证物（单测） | E2E（待跑） | 结果（日期） |
|---|---|---|---|
| @KG-S1 | `CreateKnowledgeGraphCommandHandlerTests.Handle_WhenEnabledAndAdmin_CreatesGraph`（mode=managed、database=null） | kg-e2e.mjs#KG-S1a/b | 单测 PASS；E2E 未执行（2026-09-10） |
| @KG-S2 | `CreateKnowledgeGraphCommandHandlerTests.Handle_WithTemplate_MapsRelationTypeIdsToSavedEntityTypes`；`KnowledgeGraphTemplatesTests` | kg-e2e.mjs#KG-S2a…g | 单测 PASS；E2E 未执行 |
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

## v2 前端映射（vitest）

| 验证物 | 覆盖 | 结果 |
|---|---|---|
| `ui/src/pages/knowledgegraph/__tests__/KnowledgeGraphDetail.test.tsx` | 五段菜单、托管图默认图览（调 canvas 接口）、接入图只读徽标 | PASS（2026-09-14） |
| `ui/src/pages/knowledgegraph/__tests__/KnowledgeGraphEntities.test.tsx` / `KnowledgeGraphRelations.test.tsx` | Admin 可写、Member 只读（写入口不渲染）、能力未开启不渲染 | PASS（2026-09-14） |
| `ui/src/pages/settings/__tests__/Settings.test.tsx` | KG_* 设置键、方言保存、关闭时不提交连接信息 | PASS（2026-09-14） |

## 备注

- `Handle_WhenDisabled_Throws409` / `Handle_WhenEnabledButUriEmpty_Throws409` / `CreateEntityType_WhenSettingsDisabled_Throws409` 覆盖能力门禁（对应 SDD §6，未单列场景编号）。
- `AuthorizeManagedAsync` 的只读分支同时约束 schema 与节点 / 边写 Handler，@KG-S12 两个 E2E 检查分别验证节点与实体类型写被拒。
- E2E 待具备 Neo4j 环境后在 [sop.md](./sop.md#4-验收流程) 记录执行结果。
