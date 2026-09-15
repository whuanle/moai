# publication 上架审核模块 验证映射（TDD）

> 关联：[SDD](./sdd.md) ｜ [BDD](./bdd.md) ｜ [TDD](./tdd.md) ｜ [SOP](./sop.md)
> 回归命令：后端运行中执行 `node local-dev/publication-e2e.mjs`（存量库需先执行 [asserts/publication_review.sql](../../asserts/publication_review.sql)）。

| 场景 | 验证物 | 结果（日期） |
|---|---|---|
| @PB-S1~S3 | local-dev/publication-e2e.mjs（PB-01~04） | PASS 34/34（2026-09-15） |
| @PB-S4~S5 | local-dev/publication-e2e.mjs（PB-05、06、14、15） | PASS 34/34（2026-09-15） |
| @PB-S6~S8 | local-dev/publication-e2e.mjs（PB-07~09） | PASS 34/34（2026-09-15） |
| @PB-S9 | local-dev/publication-e2e.mjs（PB-10、11） | PASS 34/34（2026-09-15） |
| @PB-S10 | local-dev/publication-e2e.mjs（PB-12） | PASS 34/34（2026-09-15） |
| @PB-S11 | local-dev/publication-e2e.mjs（PB-13）+ app-e2e.mjs（AP-13o2/o3、13p-r） | PASS 34/34 + 101/101（2026-09-15） |
| @PB-S12 | @manual（浏览器走查，见 sop.md 第 3 节） | PASS 走查（2026-09-15） |
| @PB-S13 | @manual（浏览器走查，见 sop.md 第 3 节）+ AppConfigSection.test.tsx / TeamApps.test.tsx | PASS 7/7 + 6/6（2026-09-15） |

## 证据摘要（2026-09-15）

- 构建：`dotnet build src/MoAI/MoAI.csproj` → 0 error / 0 warning（新增 publication 三个项目并入 MoAI.sln）。
- 存量库：`asserts/publication_review.sql` 已应用于开发库（publication_review 表 + 待审核唯一过滤索引 + 团队索引）。
- E2E：`node local-dev/publication-e2e.mjs` → 34 pass / 0 fail；`node local-dev/app-e2e.mjs` → 101 pass / 0 fail（公开场景已改走申请+审批）。
- OpenAPI：`/openapi/v1.json` 出现 5 个 publication 路由（apply/withdraw/review/list/team_list），`npm run syncapi` 已重新生成客户端。
- 前端：`npm run typecheck` 通过；`npm run lint` 0 error；`npm run test` 259 通过（受影响断言已同步更新）。
