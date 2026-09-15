# publication 上架审核模块 操作手册（SOP）

> 关联：[SDD](./sdd.md) ｜ [BDD](./bdd.md)（验收场景编号） ｜ [TDD](./tdd.md)（验证映射与回归命令） ｜ [SOP](./sop.md)

## 1. 日常操作：上架审批流

1. 团队 Admin+ 在应用「配置」分区点击「申请上架」（或调 `POST /api/publication/apply`，`resourceType=app|prompt` + `resourceId`）。
2. 系统管理员在侧边栏「审批上架」菜单查看待审核申请（也可调 `GET /api/publication/list`），按状态/资源类型筛选。
3. 审批：通过（`POST /api/publication/review`，`isApprove=true`）→ 系统**自动**将目标资源 `is_public` 置为 true；驳回（`isApprove=false`，可附审批意见）→ 资源保持未公开。
4. 团队侧在申请未被审批前可「撤回申请」（待审核状态专用，软删除记录）。

> 前置：应用需先「发布」（发布 ≠ 公开，公开列表只展示已发布 + 已公开的应用）；外部应用不支持公开。

## 2. 环境准备与回归

1. 新库由 `EnsureCreated` 自动建 `publication_review` 表；**存量库**执行：
   `docker exec -i moai-postgres psql -U postgres -d moai < asserts/publication_review.sql`
2. 回归：后端运行中执行 `node local-dev/publication-e2e.mjs`（34 项）；应用公开相关回归 `node local-dev/app-e2e.mjs`（101 项，AP-13a-d/o2/o3/p-r 覆盖公开链路）。
3. 前端改动后需 `npm run syncapi` 重新生成客户端（Kiota 锁 `1.0.0-preview.93`）。

## 3. 页面验收走查（@PB-S12 / @PB-S13）

1. admin 登录 → 侧边栏出现「审批上架」一级菜单 → 进入 `/publications`，看到全平台申请列表，状态/类型筛选与刷新可用。
2. 对一条待审核记录：通过需二次确认；驳回弹窗（不可点遮罩关闭）可填审批意见；通过后该资源在应用广场可见、应用配置分区显示「已公开」。
3. 团队账号登录 → 应用「配置」分区「上架状态」：未公开可申请（弹窗填说明）→ 审核中可撤回 → 驳回后可重新申请；新建应用弹窗无公开开关。
4. 非管理员看不到「审批上架」菜单，直接访问 `/publications` 被重定向到概览页。

## 4. 排障表

| 现象 | 原因与处理 |
|---|---|
| 申请返回 409「已有待审核的上架申请」 | 同资源 pending 唯一约束生效：等待审批或先撤回旧申请 |
| 申请返回 400「外部应用不支持公开到平台」 | 外部应用面向外部用户/匿名，不走平台公开；改用内部应用 |
| 审批通过返回 404「应用不存在或已删除」 | 资源在申请后被删除，无法通过；改走驳回 |
| 存量库启动后接口 500 报关系不存在 | 未执行第 2 节 DDL；EnsureCreated 对存量库不补新表 |
| 审批接口 403 | 仅系统管理员（user 表 is_admin）可审批与查全平台列表 |
| 通过了但应用广场看不到 | 广场只展示「已发布 + 已公开 + 未禁用」的内部应用，确认应用已发布 |
