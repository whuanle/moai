# 组织（Org，ORG）验证映射（TDD）

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
| @ORG-S29 ~ @ORG-S33 | [OrgRolesPanel.test.tsx](../../ui/src/pages/admin/__tests__/OrgRolesPanel.test.tsx) | PASS 10/10（2026-11-02，节点图标操作组/成员前端筛选重构后复跑） |
| @ORG-S34 | @manual 浏览器走查（[SOP 第 2 节](./sop.md)） | PASS（2026-10-28） |
| @ORG-S35 | [local-dev/org-e2e.mjs](../../local-dev/org-e2e.mjs)（ORG-54：端点下线 400 + 列表无默认标记；ORG-33~38 退役不复用） | PASS 53/53（2026-11-02） |
| @ORG-S42 | @manual 浏览器走查（前端 path 前缀拦截；后端防环 @ORG-S16 兜底；间隙=同级排序、边缘自动滚动随 290/292 轮改版） | PASS（2026-11-02） |
| @ORG-S43 | [local-dev/org-e2e.mjs](../../local-dev/org-e2e.mjs)（ORG-39） | PASS 53/53（2026-11-02） |
| @ORG-S44 | 同上（ORG-40/41） | PASS（2026-11-01） |
| @ORG-S45 | 同上（ORG-42/43；单测 Replace_OverwritesAndClears/Duplicate/Missing 佐证） | PASS（2026-11-01） |
| @ORG-S46 | 同上（ORG-44；单测 ScopeSelf/NoGrant_Denies 佐证） | PASS（2026-11-01） |
| @ORG-S47 | 同上（ORG-45；单测 ScopeUp_IncludesAncestors 佐证） | PASS（2026-11-01） |
| @ORG-S48 | 同上（ORG-46/47/48；单测 ScopeDown/ListAllowedRoles/Users 佐证） | PASS（2026-11-01） |
| @ORG-S49 | 同上（ORG-47/48） | PASS（2026-11-01） |
| @ORG-S50 | 同上（ORG-49/50；单测 MultiRecords_Union/NoGrant 佐证） | PASS（2026-11-01） |
| @ORG-S51 | 同上（ORG-51/52；单测 RoleMove_ReevaluatesByNewPath 佐证） | PASS（2026-11-01） |
| @ORG-S52 | 同上（ORG-53；单测 DeleteRole_CascadesGrantsAndBindings 佐证） | PASS（2026-11-01） |
| @ORG-S54 | 同上（ORG-55；创建追加同级末尾） | PASS 53/53（2026-11-02） |
| @ORG-S55、@ORG-S56 | 同上（ORG-56/57/59：置顶、插位、跨级移动 path 重写；单测 ReorderToFirst/InsertAfterSibling/MoveAcrossLevel 佐证） | PASS（2026-11-02） |
| @ORG-S57 | 同上（ORG-58；单测 PrevNotSibling/SelfAsPrev/MoveIntoOwnDescendant 佐证） | PASS（2026-11-02） |

## 后端单测补充

| 用例 | 验证物 | 结果（日期） |
|---|---|---|
| 创建/嵌套 path/父 404/同父重名 409/校验器 | [MoAI.Org.Tests](../../tests/MoAI.Org.Tests/)（Create 9 例） | PASS 41/41（2026-10-28） |
| 改名不动 path/子树迁移重写/移到根/防环 400/404 | 同上（Update 8 例） | PASS（2026-10-28） |
| 删除保护与级联解绑 | 同上（Delete 3 例） | PASS 44/44（2026-11-02，默认组织相关用例随功能下线删除） |
| 用户组织覆盖/去重/404/400 | 同上（UserRoles 5 例） | PASS（2026-10-28） |
| 列表数值层级序/计数/搜索/双向回显 | 同上（Query 6 例） | PASS（2026-11-02，IsDefault 断言随功能下线移除） |
| 列表同级按 sort_order DFS 树序输出 | 同上（Query 7 例：List_OrdersSiblingsBySortOrder） | PASS 52/52（2026-11-02） |
| 排序：置顶重编号/插位/参照校验/跨级移动落位与 path 重写/防环/404 | 同上（UpdateOrgRoleOrderCommandHandler 7 例） | PASS 52/52（2026-11-02） |
| 统一组织授权：self/up/down 语义矩阵/并集/覆盖清空/Guid 资源/反查资源/树迁移重判/删除级联 | 同上（OrgGrantServiceTests 14 例，假缓存注入） | PASS 52/52（2026-11-02） |

## 回归命令

```bash
node local-dev/org-e2e.mjs            # 需后端运行中（默认 :5000，可用参数/ORG_BASE 覆盖），零外部依赖
dotnet test tests/MoAI.Org.Tests      # 后端单测（sqlite 内存库）
cd ui && npx vitest run src/pages/admin/__tests__/OrgRolesPanel.test.tsx src/pages/admin/__tests__/AdminTeams.test.tsx
```

## 覆盖率说明

- 写路径全部自动化；@ORG-S2（未登录 401）由全局 JWT 拦截保证，走查确认。
- path 列长上限（255）防线在 `OrgRoleTreeGuard.EnsurePathLength`，仅超长拒绝场景未做自动化（需构造 20+ 层深树，成本高），由常量约束与创建/迁移场景佐证。
- @ORG-S53 成员筛选为纯前端行为，由 [OrgRolesPanel.test.tsx](../../ui/src/pages/admin/__tests__/OrgRolesPanel.test.tsx)「成员查询按钮按用户名/昵称前端筛选」用例覆盖（断言不触发成员接口）。
