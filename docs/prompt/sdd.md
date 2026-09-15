# 提示词（Prompt）设计规格（SDD）

> 关联：[SDD](./sdd.md) ｜ [BDD](./bdd.md) ｜ [TDD](./tdd.md) ｜ [SOP](./sop.md) ｜ 上游：[../publication/sdd.md](../publication/sdd.md) ｜ 证据：[local-dev/prompt-e2e.mjs](../../local-dev/prompt-e2e.mjs)
> 规范：[../DOC-STANDARD.md](../DOC-STANDARD.md)。行为场景见 BDD，本文只写设计。

## 目标

平台级提示词库：任何用户可创建**个人提示词**（仅本人使用）；团队 Admin 及以上可创建**团队提示词**（默认仅团队内使用）；两类提示词均可申请上架，系统管理员审批通过后进入**提示词市场**对所有用户开放。

## 组件

- `src/prompt/MoAI.Prompt.Shared|Core|Api` 三层，Maomi 模块 `PromptCoreModule` 注册进 `MainModule`，依赖 `Team.Shared`（角色事实）、`Account.Shared`（人名填充）、`Classify.Shared`（分类类型常量）。
- 接口（Controller 门禁外全部在 Handler 校验资源归属/角色）：
  - `POST /api/prompt`、`PUT /api/prompt/{id}`、`DELETE /api/prompt/{id}`
  - `POST /api/prompt/{id}/avatar`（设置头像，objectKey 需已登记，权限同更新）
  - `GET /api/prompt/my_list`（个人）、`GET /api/prompt/team_list`（团队成员）、`GET /api/prompt/{id}`（详情含内容）、`GET /api/prompt/market_list`（公开）
- 上架复用 [publication 模块](../publication/sdd.md)（`resource_type=prompt`，`resource_id=prompt.id` 数字字符串）；本模块仅消费，不改其状态机。

## 数据

沿用既有 `prompt` 表（`PromptEntity`，用户已建）：`team_id`（0=个人）、`name`(≤20)、`description`(≤255)、`content`(text，入参限 10000)、`prompt_class_id`（classify.type=prompt）、`is_public`（上架审批通过置 true）、`counter`（市场被他人查看次数）、`is_audit`（预留未用）。存量库执行 `asserts/prompt.sql`。

## 关键决策

1. **归属即权限**：`team_id=0` 时仅 `create_user_id` 本人可读写；`team_id>0` 时经 `ITeamService.GetMyRoleAsync` 要求 Admin+ 可管理、成员可读。列表/详情均按此过滤，无权限统一 404 防探测。
2. **个人上架走创建人门禁**：publication 的 apply/withdraw 原只认团队角色，`team_id=0` 时新增「`create_user_id == ContextUserId`」分支，团队提示词仍走 Admin+。
3. **待审核申请冗余回显**：`PromptItem.PendingPublicationId` 由列表查询批量联查 `publication_review`（pending）得到，前端据此显示「待审核」并直接撤回，避免个人提示词无 team_list 可查的死路。
4. **删除联动**：删除提示词同时移除其 pending 上架申请（与撤回同语义），防审核列表僵尸记录；已审批通过的记录保留历史。
5. **counter 语义**：仅当查看者既非创建人又非团队成员（即通过市场可见路径）时 +1，创建人/团队成员查看不计。
6. **IUserIdContext 传递**：所有需用户维度的命令实现 `IUserIdContext`，Controller 内 new 的命令显式 `SetUserContext`（全局 `AutoAssignUserIdFilter` 只覆盖绑定参数）。

## 前端

- 一级菜单「我的提示词」`/prompts`（个人列表 + 上架/撤回）与「提示词市场」`/prompt-market`（浏览/详情复制/计数）。
- **独立编辑器页** `PromptEditor`：新建/编辑不再用弹窗，路由 `/prompts/new`、`/prompts/:promptId/edit`、`/team/:teamId/prompt/new`、`/team/:teamId/prompt/:promptId/edit`；左栏 Markdown 编辑（等宽 TextArea）、右栏 `react-markdown + remark-gfm` 实时预览；头像在编辑器内上传（新建先传存储拿 objectKey 随创建提交，编辑直接调 avatar 接口）。
- 团队详情「提示词」分区（成员可见，`TeamPrompts` 按 `canManage` 收敛管理入口）。
- 共享 `PromptDetailModal`（头像 + Markdown 渲染内容）供我的/团队/市场三处复用。
- 封装层 `ui/src/api/prompt.ts`，头像走 `uploadImageWithKey`（存储三段直传）+ `setPromptAvatar`；上架复用 `ui/src/api/publication.ts`；分类选项复用 `classifyApi.getClassifies('prompt')`。

## 关键决策（补充）

7. **头像存 objectKey**：与团队/用户头像同规则——数据库只存 `avatar_path`（objectKey 或绝对地址），前端 `resolveStorageUrl` 拼展示地址；后端校验 objectKey 必须是已完成上传并登记（`files.is_uploaded`）的文件，防伪造。

## 已知问题

- 应用 Agent 配置的 Prompt 仍是自由文本，尚未接入提示词库选择器（后续独立需求）。
- `is_audit` 字段为建表遗留，业务未使用。
- 提示词列表无分页（与 variable/publication 同策略），数据量大时需补。
