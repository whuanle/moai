# 流程应用模块验证映射（TDD）

> 关联：[SDD](./sdd.md) ｜ [BDD](./bdd.md) ｜ [TDD](./tdd.md) ｜ [SOP](./sop.md) ｜ 证据：[local-dev/workflow-e2e.mjs](../../local-dev/workflow-e2e.mjs)

| 场景 | 验证物 | 结果（日期） |
|---|---|---|
| @WF-S1 | local-dev/workflow-e2e.mjs（WF-01） | PASS 27/27（2026-09-16） |
| @WF-S2 | local-dev/workflow-e2e.mjs（WF-04/06） | PASS 27/27（2026-09-16） |
| @WF-S3 | local-dev/workflow-e2e.mjs（WF-02/03） | PASS 27/27（2026-09-16） |
| @WF-S4 | local-dev/workflow-e2e.mjs（WF-07） | PASS 27/27（2026-09-16） |
| @WF-S5 | local-dev/workflow-e2e.mjs（WF-08） | PASS 27/27（2026-09-16） |
| @WF-S6 | local-dev/workflow-e2e.mjs（WF-09） | PASS 27/27（2026-09-16） |
| @WF-S7 | local-dev/workflow-e2e.mjs（WF-10） | PASS 27/27（2026-09-16） |
| @WF-S8 | local-dev/workflow-e2e.mjs（WF-11/12） | PASS 27/27（2026-09-16） |
| @WF-S9 | local-dev/workflow-e2e.mjs（WF-13） | PASS 27/27（2026-09-16） |
| 引擎行为（条件路由/跳过传播/恢复/插值/校验） | tests/MoAI.App.Workflow.Tests（12 用例） | PASS 12/12（2026-09-16） |
| 设计器转换层（往返/条件端口/校验） | ui/src/pages/teams/apps/workflow/__tests__/utils.test.ts（10 用例） | PASS 10/10（2026-09-16） |
| 前端回归 | ui `npm run typecheck && npm run lint && npm run test` | 0 error / 0 error / 299/299（2026-09-16） |
| 后端构建 | `dotnet build src/MoAI/MoAI.csproj` | 0 error（2026-09-16） |

## 验证前置

1. 存量库执行 DDL：`psql -f asserts/app_workflow.sql`（新库由 EnsureCreated 自动建）。
2. 后端运行中：`cd src/MoAI && dotnet run`。
3. 执行：`node local-dev/workflow-e2e.mjs`。
