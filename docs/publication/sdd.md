# publication 上架审核模块 设计规格（SDD）

> 关联：[SDD](./sdd.md) ｜ [BDD](./bdd.md) ｜ [TDD](./tdd.md) ｜ [SOP](./sop.md) ｜ 上游：[../app/sdd.md](../app/sdd.md)（应用 is_public）、[../team/sdd.md](../team/sdd.md)（角色） ｜ 证据：[local-dev/publication-e2e.mjs](../../local-dev/publication-e2e.mjs)、[asserts/publication_review.sql](../../asserts/publication_review.sql)
> 规范：[../DOC-STANDARD.md](../DOC-STANDARD.md)。行为场景见 BDD（@PB-Sxx），本文不重复。

## 目标

资源公开到平台（`is_public=true`）改为**审批制**：团队申请上架 → 进入上架审核表 → 系统管理员在「审批上架」菜单统一审批 → **审批通过后由系统将目标资源 `is_public` 置为 true**。应用与提示词均走此流程；后续新资源类型扩展枚举即可接入。

## 组件

```
src/publication/                  本模块（CQRS 三层）
├── MoAI.Publication.Shared/      Commands/Queries + Responses（枚举复用 Database.Enums）
├── MoAI.Publication.Core/        Handlers(3) + Queries(2) + 列表映射辅助
└── MoAI.Publication.Api/         Controllers/PublicationController（/api/publication，管理员门禁在 Controller）
src/database/MoAI.Database.Shared/   PublicationReviewEntity + PublicationResourceType/PublicationState 枚举
src/database/MoAI.Database.Postgres/ publication_review 表配置（EnsureCreated 建表）
ui/src/pages/publications/        审批上架管理页（adminNav 一级菜单 /publications）
ui/src/api/publication.ts         手写封装层（Kiota 生成物之外）
```

## 数据

`publication_review`（实体 `PublicationReviewEntity : IFullAudited`，存量库执行 [asserts/publication_review.sql](../../asserts/publication_review.sql)）：

| 列 | 说明 |
|---|---|
| id | 自增主键（bigint identity） |
| resource_type | int，见 `PublicationResourceType`（app=0，prompt=1） |
| resource_id | varchar(64)，**字符串统一承载**：应用为 uuid、提示词为数字 id（两类主键类型不同） |
| resource_name | varchar(100)，申请时的名称快照（资源后续改名/删除不影响审批记录可读性） |
| team_id | 资源所属团队（冗余存储，供团队侧列表与管理员展示） |
| apply_reason / review_comment / review_time | 申请说明、审批意见、审批时间 |
| state | int，见 `PublicationState`（pending=0，approved=1，rejected=2） |
| create_user_id / update_user_id | 审计自动注入：申请人 / 审批人（无需单独 reviewer 列） |

约束：`ux_publication_review_resource_pending (resource_type, resource_id) WHERE is_deleted = 0 AND state = 0` —— 同一资源同时只有一条待审核申请；撤回走软删除即释放该约束，驳回保留记录、可重新申请。

## 关键决策

- **is_public 只有一个写入口**：应用创建/更新命令已移除 `IsPublic` 字段（[AP-S14](../app/bdd.md#ap-s14)）；审批通过时由 `ReviewPublicationCommandHandler` 在同一事务内写目标资源 `is_public=true` 与审核记录。提示词实体已有 `is_public`，暂无业务层，审批链路已支持。
- **resource_id 用字符串**：AppEntity.Id 是 Guid、PromptEntity.Id 是 int，Handler 内按资源类型解析并校验，避免两列或触发器。
- **管理员门禁在 Controller**：`PublicationController.EnsureAdminAsync`（`IUserAccountService.GetUserStateAsync().IsAdmin`，403），Handler 只做团队角色与状态机校验，符合 CQRS 约定。
- **审批终态校验**：非 pending 记录审批/撤回返回 409；通过时目标资源已被删除返回 404（引导管理员改走驳回）。
- **列表展示**：响应项继承 `AuditsInfo` 并经 `IUserInfoFillService.FillAsync` 填充申请人/审批人姓名，团队名称批量查询补齐；管理员列表 `/api/publication/list`、团队列表 `/api/publication/team_list`（复用同一响应模型）。
- **前端**：管理页在 `adminNav` 一级菜单（仅 isAdmin 可见，页面内再校验）；应用配置分区以「上架状态」替代原公开开关（申请/撤回/重新申请入口 + 状态标签）。

## 已知问题

- 提示词尚无 CRUD 业务层（[classify/sop](../classify/sop.md) 注释同），申请/审批链路就绪但无法从 API 创建提示词；E2E 覆盖 404 分支。
- 资源被删除后已有 approved 记录不回滚 is_public（记录保留历史）；驳回/撤回不受影响。
- 审批列表暂不分页（按创建时间倒序全量返回，前端分页），量大时再补 pageNo/pageSize。
