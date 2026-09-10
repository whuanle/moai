# 知识图谱模块验证映射（TDD）

> 关联：[SDD](./sdd.md) ｜ [BDD](./bdd.md) ｜ [TDD](./tdd.md) ｜ [SOP](./sop.md) ｜ 证据：[local-dev/kg-e2e.mjs](../../local-dev/kg-e2e.mjs) ｜ 单测：[tests/MoAI.KnowledgeGraph.Tests/](../../tests/MoAI.KnowledgeGraph.Tests/)

## 自检记录

- 构建：`dotnet build src/MoAI/MoAI.csproj` → **0 错误**（2026-09-10）
- 单测：`dotnet test tests/MoAI.KnowledgeGraph.Tests/MoAI.KnowledgeGraph.Tests.csproj` → **PASS 33/33**（2026-09-10，覆盖模板映射、探活 / 内省、能力门禁、角色判定、只读拦截、类型引用拦截、分页钳制、删图清库分支）
- E2E：`node local-dev/kg-e2e.mjs` → **未执行（PENDING）**。脚本需后端 + Neo4j 可达且 `OPEN_NEO4J=true`；当前环境无 Neo4j 实例，脚本会打印 `SKIP` 并退出码 0（CI 友好），故不记为 PASS。下表 E2E 列均为待跑，单测列已绿。
- 前端：本期后端交付，`ui/src/pages/kg` 与 `ui/src/api/knowledgeGraph.ts` 未落地，无 vitest 映射。

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

## 备注

- `Handle_WhenDisabled_Throws409` / `Handle_WhenEnabledButUriEmpty_Throws409` / `CreateEntityType_WhenSettingsDisabled_Throws409` 覆盖能力门禁（对应 SDD §6，未单列场景编号）。
- `AuthorizeManagedAsync` 的只读分支同时约束 schema 与节点 / 边写 Handler，@KG-S12 两个 E2E 检查分别验证节点与实体类型写被拒。
- E2E 待具备 Neo4j 环境后在 [sop.md](./sop.md#4-验收流程) 记录执行结果。
