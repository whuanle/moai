# 角色组织（Org，ORG）验证映射（TDD）

> 关联：[SDD](./sdd.md) ｜ [BDD](./bdd.md)（场景定义） ｜ [TDD](./tdd.md) ｜ [SOP](./sop.md)
> 本文只做「场景 → 验证物 → 结果」映射；场景语义见 BDD 编号，设计依据见 SDD。

## 场景映射表

| 场景 | 验证物 | 结果（日期） |
|---|---|---|
| @ORG-S1 | [local-dev/org-e2e.mjs](../../local-dev/org-e2e.mjs)（ORG-01/02/03） | PASS 32/32（2026-10-28） |
| @ORG-S2 | @manual HTTP 走查（全局 JWT 拦截保证） | PASS（2026-10-28） |
| @ORG-S3、@ORG-S4 | 同上（ORG-04/05） | PASS（2026-10-28） |
| @ORG-S5 ~ @ORG-S7 | 同上（ORG-06/07/08/09） | PASS（2026-10-28） |
| @ORG-S8、@ORG-S9 | 同上（ORG-10/11；单测 DuplicateName/SameNameUnderDifferentParent 佐证） | PASS（2026-10-28） |
| @ORG-S10 | 同上（ORG-12；单测 List_ReturnsPathOrderWithCounts 数值序判别佐证） | PASS（2026-10-28） |
| @ORG-S11 | 同上（ORG-13） | PASS（2026-10-28） |
| @ORG-S12 | 同上（ORG-14） | PASS（2026-10-28） |
| @ORG-S13 | 同上（ORG-15；单测 RenameOnly_PathUnchanged 佐证） | PASS（2026-10-28） |
| @ORG-S14 | 同上（ORG-16；单测 MoveSubtree_RewritesAllDescendantPaths 佐证） | PASS（2026-10-28） |
| @ORG-S15 | 同上（ORG-17；单测 MoveToRoot_PathBecomesTopLevel 佐证） | PASS（2026-10-28） |
| @ORG-S16、@ORG-S17 | 同上（ORG-18/19；单测 UnderOwnDescendant/SelfAsParent 佐证） | PASS（2026-10-28） |
| @ORG-S18 | 同上（ORG-20） | PASS（2026-10-28） |
| @ORG-S19、@ORG-S20 | 同上（ORG-21/22） | PASS（2026-10-28） |
| @ORG-S21 | 同上（ORG-23/24；单测 UserRoles_ReturnsPathOrderWithUserInfo 佐证） | PASS（2026-10-28） |
| @ORG-S22 | 同上（ORG-25；单测 Assign_DeduplicatesInput 佐证） | PASS（2026-10-28） |
| @ORG-S23 | 同上（ORG-26/27） | PASS（2026-10-28） |
| @ORG-S24 | 同上（ORG-28；单测 Assign_ThenOverride/EmptyList_ClearsAll 佐证） | PASS（2026-10-28） |
| @ORG-S25 ~ @ORG-S27 | 同上（ORG-29 ~ ORG-32；单测 HasChildren_400/DeleteLeaf_RemovesRoleAndUnbindsUsers 佐证） | PASS（2026-10-28） |
| @ORG-S28 | [AdminTeams.test.tsx](../../ui/src/pages/admin/__tests__/AdminTeams.test.tsx)（非管理员重定向） | PASS 3/3（2026-10-28） |
| @ORG-S29 ~ @ORG-S33 | [OrgRolesPanel.test.tsx](../../ui/src/pages/admin/__tests__/OrgRolesPanel.test.tsx) | PASS 10/10（2026-10-29，昵称主显示名调整后复跑） |
| @ORG-S34 | @manual 浏览器走查（[SOP 第 2 节](./sop.md)） | PASS（2026-10-28） |
| @ORG-S35 ~ @ORG-S40 | [local-dev/org-e2e.mjs](../../local-dev/org-e2e.mjs)（ORG-33 ~ ORG-38；默认角色钩子含真实注册链路） | PASS 38/38（2026-10-29） |
| @ORG-S41 | [OrgRolesPanel.test.tsx](../../ui/src/pages/admin/__tests__/OrgRolesPanel.test.tsx)（设为默认/取消默认） | PASS（2026-10-29） |
| @ORG-S42 | @manual 浏览器走查（前端 path 前缀拦截；后端防环 @ORG-S16 兜底） | PASS（2026-10-29） |

## 后端单测补充

| 用例 | 验证物 | 结果（日期） |
|---|---|---|
| 创建/嵌套 path/父 404/同父重名 409/校验器 | [MoAI.Org.Tests](../../tests/MoAI.Org.Tests/)（Create 9 例） | PASS 41/41（2026-10-28） |
| 改名不动 path/子树迁移重写/移到根/防环 400/404 | 同上（Update 8 例） | PASS（2026-10-28） |
| 删除保护与级联解绑/删默认角色清设置/非默认保留 | 同上（Delete 5 例） | PASS（2026-10-28） |
| 用户角色覆盖/去重/404/400 | 同上（UserRoles 5 例） | PASS（2026-10-28） |
| 列表数值层级序/计数/搜索/双向回显/IsDefault | 同上（Query 6 例） | PASS（2026-10-28） |
| 默认角色钩子：入列语义/无默认/非法值/角色缺失/已有绑定 | 同上（Hook 5+3 例） | PASS（2026-10-28） |
| 设置/清空默认（404、经设置服务 upsert） | 同上（UpdateOrgRoleDefault 3 例） | PASS（2026-10-28） |

## 回归命令

```bash
node local-dev/org-e2e.mjs            # 需后端运行中（默认 :5000，可用参数/ORG_BASE 覆盖），零外部依赖
dotnet test tests/MoAI.Org.Tests      # 后端单测（sqlite 内存库）
cd ui && npx vitest run src/pages/admin/__tests__/OrgRolesPanel.test.tsx src/pages/admin/__tests__/AdminTeams.test.tsx
```

## 覆盖率说明

- 写路径全部自动化；@ORG-S2（未登录 401）由全局 JWT 拦截保证，走查确认。
- path 列长上限（255）防线在 `OrgRoleTreeGuard.EnsurePathLength`，仅超长拒绝场景未做自动化（需构造 20+ 层深树，成本高），由常量约束与创建/迁移场景佐证。
- @ORG-S38 的 OAuth 自动建号链路由代码路径保证（OAuth 注册内部复用 `RegisterUserCommand`，钩子在 OAuth 外层事务内执行），E2E 以 HTTP 注册链路验证钩子本体。
