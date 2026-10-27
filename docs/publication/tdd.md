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
| @PB-S16 | local-dev/publication-e2e.mjs（PB-02c/c2 不携带版本号申请 200 且版本自动取当前版本并撤回 + PB-02d/e/f 非法格式/非应用带版本/低于当前版本 400 + PB-07a 团队列表回显 publishVersion=10200） | PASS 49/49（2026-10-27，上架不再单独填版本号：缺省自动取应用当前版本） |
| @PB-S17 | local-dev/publication-e2e.mjs（PB-16a 待审核全部排在已审批前 + PB-16b 管理员列表组内倒序 + PB-16c 团队列表首条为最新待审核） | PASS 40/40（2026-10-26，对 5031 新构建实例） |
| @PB-S18 | local-dev/publication-e2e.mjs（PB-17a Member 下架 403 + PB-17b 非成员 404 + PB-17c 下架 200 + PB-17d is_public=false + PB-17e 移出公开列表 + PB-17f 未公开 409 + PB-17g 可重新申请） | PASS 47/47（2026-10-27，新增 `POST /api/publication/takedown`，syncapi 已重新生成客户端） |
| @PB-S19 | @manual（个人提示词/技能下架门禁浏览器走查） | 待走查（2026-10-27） |

## 证据摘要（2026-10-27 增量：下架）

- 后端：`TakedownPublicationCommand`（Shared）+ `TakedownPublicationCommandHandler`（Core，团队资源 Admin+/个人资源仅创建人/未公开 409）+ `PublicationController` 新增 `POST /publication/takedown`；`dotnet build src/MoAI/MoAI.csproj` 0 error。
- 应用联动：`UnpublishAppCommandHandler` 撤回发布时清 `is_public` 并撤下待审核申请（见 [../app/bdd.md @AP-S96](../app/bdd.md#ap-s96)，app-e2e AP-96a~h 8/8）。
- OpenAPI：`/openapi/v1.json` 出现 `takedown` 路由，`npm run syncapi` 重新生成客户端。
- 前端：AppPublicationSection（下架按钮/撤回发布改名）+ `ui/src/i18n` zh/en 同步；AppPublicationSection.test 12/12（含下架与成员只读）。
