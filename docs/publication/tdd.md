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
| @PB-S14 | local-dev/publication-e2e.mjs（PB-18a 未发布申请 400 + PB-18b 发布后申请 200 + PB-18c 撤回） | PASS 77/77（2026-11-04，上架前置校验） |
| @PB-S15 | local-dev/publication-e2e.mjs（PB-19a 清单为空 400 + PB-19b 组织不存在 400 + PB-19c 非应用携带 400 + PB-19d 同组织重复 400） | PASS 77/77（2026-11-04） |
| @PB-S20 | local-dev/publication-e2e.mjs（PB-20a 组织树创建 + PB-20b 用户分配组织 + PB-20c/d 申请与回显范围组织名 + PB-20e/f 审批通过 is_public + PB-20g/h 广场范围内可见范围外不可见 + PB-20i/j 直链建会话 404/200） | PASS 77/77（2026-11-04，publicApp 物化 + PublicAppScopeService 门禁） |
| @PB-S21 | local-dev/publication-e2e.mjs（PB-21a/b 下架清范围 + PB-21c/d 全员重申审批 + PB-21e/f 组织外恢复可见可用） | PASS 77/77（2026-11-04） |
| @PB-S22 | local-dev/publication-e2e.mjs（PB-22a 已公开重复申请 409 + PB-22b/c 停用清快照联动下架 + PB-22d 广场不可见 + PB-22e 停用后申请 400） | PASS 77/77（2026-11-04） |
| @PB-S23 | local-dev/publication-e2e.mjs（PB-23a 未登录 401 + PB-23b 非管理员 403 + PB-23c 申请 200 + PB-23d 申请后数量 +1 + PB-23e 审批通过后回落） | PASS 82/82（2026-11-04，新增 `GET /api/publication/pending-count`，syncapi 已重新生成客户端） |

## 证据摘要（2026-10-27 增量：下架）

- 后端：`TakedownPublicationCommand`（Shared）+ `TakedownPublicationCommandHandler`（Core，团队资源 Admin+/个人资源仅创建人/未公开 409）+ `PublicationController` 新增 `POST /publication/takedown`；`dotnet build src/MoAI/MoAI.csproj` 0 error。
- 应用联动：`UnpublishAppCommandHandler` 撤回发布时清 `is_public` 并撤下待审核申请（见 [../app/bdd.md @AP-S96](../app/bdd.md#ap-s96)，app-e2e AP-96a~h 8/8）。
- OpenAPI：`/openapi/v1.json` 出现 `takedown` 路由，`npm run syncapi` 重新生成客户端。
- 前端：AppPublicationSection（下架按钮/撤回发布改名）+ `ui/src/i18n` zh/en 同步；AppPublicationSection.test 12/12（含下架与成员只读）。

## 证据摘要（2026-11-04 增量：上架前置 + 可视范围）

- 后端：`publication_review` 加 `visible_scope`/`visible_org_grants` 两列（EF 配置 + 启动幂等 DDL 补列）；`ApplyPublicationCommand` 加 VisibleScope/VisibleOrgGrants（仅应用、清单非空/组织存在/同组织唯一）；`OrgGrantResourceType` 新增 `PublicApp=6` + `AppVisibleScope` 枚举；审批通过物化/下架与停用清空 `org_grant`（时序：校验与 SaveChanges 之后）；App.Core 新增 `PublicAppScopeService`，市场列表过滤 + 公开应用使用门禁追加范围判定。`dotnet build` 0 error。
- 踩坑修复：清授权最初放在 Takedown handler 角色校验之前，授权服务内部 SaveChanges 连带持久化了未授权调用的 `IsPublic=false` 中间状态（PB-17b/c 409）——已移至鉴权与保存之后，E2E 复绿。
- OpenAPI：`/publication/apply` 请求体与 review 列表项新增 visibleScope/visibleOrgGrants，`npm run syncapi` 重新生成客户端。
- 前端：申请弹窗新增「可见范围」（GrantScopeEditor `hideTeams` + `buildGrantScopeRoleTree` 抽至 utils/orgScopeTree）；发布按钮语义收敛（发布常驻/停用并列，删「重新发布/撤回发布」文案）；i18n zh/en 同步；AppPublicationSection.test 17/17、AppConfigSection 28/28、AppWorkspace 5/5（按钮语义断言同步）。
- E2E：`node local-dev/publication-e2e.mjs` → **77 pass / 0 fail**（新增 PB-18~22 五组可视范围用例；组织造数走 `/api/org/role/create`、`/api/org/user/{id}/roles`）。

## 证据摘要（2026-11-04 增量二：组织授权前端组件收敛 + 申请弹窗宽度）

- 行为无变化，bdd 场景不动；纯前端重构与样式调整。
- 宽度：申请上架弹窗 `width=640`（原默认 520，授权组织行被「仅当前/以上/以下」按钮组挤压到约 216px）；GrantScopeEditor 组织下拉 `minWidth 170→220` + `popupMatchSelectWidth={false}`（下拉按内容自适应、最小为触发器宽，窄容器同样可读）。
- 组件收敛：GrantScopeEditor 组织树改组件自加载（`roleTree` prop 可选，不传时 mount 拉取一次；AppPublicationSection 删 `ensureRoleTree`、AdminAccessApps 删 `loadRoleTree` 样板）；方向类型统一收敛至 utils/orgScopeTree `GrantScopeDirection`（`api/subsystem.OrgGrantScopeType`、`api/publication.VisibleGrantDirection` 改为别名，导出名不变）；新增只读标签公共组件 `ui/src/components/VisibleScopeTag.tsx`（替换 AppPublicationSection `renderScopeTag` 与 Publications 内联渲染两份重复）。
- 验证：`npm run typecheck` 通过；`npm run lint` 0 error（零新增警告）；`npm run test` **743/743 全绿**（97 文件，含 AppPublicationSection 17 用例与 AdminAccessApps 7 用例）。
