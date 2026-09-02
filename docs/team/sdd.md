# 团队模块设计规格（SDD）

> 关联：[SDD](./sdd.md) ｜ [BDD](./bdd.md) ｜ [TDD](./tdd.md) ｜ [SOP](./sop.md) ｜ 上游：[../database-scaffold/sdd.md](../database-scaffold/sdd.md) ｜ 证据：[local-dev/team-e2e.mjs](../../local-dev/team-e2e.mjs)

- 日期：2026-09-02
- 状态：数据库 + 后端 API + 前端页面已实现（本轮范围），知识库/插件挂载为下阶段
- 领域：`src/team`（Shared/Core/Api 三层），前端 `ui/src/pages/teams`

## 1. 目标

团队是知识库、插件等资源的管理单元。本期交付：团队 CRUD、成员管理（添加/移除/角色调整/自行退出）、解散；后续资源表通过 `team_id` 挂载。对齐侧边栏既有的「团队」菜单（问题台账 P9 死菜单之一）。

## 2. 数据模型

- `team`：`id / name / description / avatar_path / is_disable` + 审计。软删除为 **boolean**（本模块特例，见决策 D1），唯一约束用 **partial 索引** `(name) WHERE is_deleted = false`。
- `team_user`：`team_id / user_id / role / ` 审计。partial 唯一 `(team_id, user_id) WHERE is_deleted = false`。
- 审计四件（create/update 人+时间）与软删除转换由 `DatabaseContext` 审计钩子自动完成，业务零代码。
- 头像存 ObjectKey，查询时经 `IStorageService.GetPublicFileUrl` 转 `/static` 公开地址（与用户头像同构）。
- DDL：`asserts/team.sql`；无 EF Migration，已有库需手动执行。

## 3. 角色与权限矩阵

| 角色 | 值 | 能力 |
|---|---|---|
| Owner | 0 | 全部 + 调整成员角色 + 解散团队；不可退出/被移除（需先解散） |
| Admin | 1 | 修改团队信息 + 添加成员（仅 Member）+ 移除 Member |
| Member | 2 | 查看团队/成员 + 自行退出 |

关键规则（Handler 层判定，依赖 team_user 事实）：

- 创建者自动成为 Owner；团队名未删除范围内唯一（重名 409）。
- 添加成员：目标必须是存在用户（404）、未在团（409）；授 Admin 仅 Owner（403）。
- 改角色：仅 Owner；不能改自己（400）；只能 Admin/Member。
- 转让所有权（TM-13）：仅 Owner；目标须在团（404）、不能是自己（400）；转让后原 Owner 降为 Admin，角色互换。
- 团队头像（TM-14）：Admin+ 可设置；objectKey 必须是 file 表已完成上传记录（404 防伪造，与用户头像同规则）；走存储直传管线。
- 移除：目标 Owner 恒 400；Admin 不可移除 Admin（403）；Member 仅可自行退出；Owner 不可退出（400）。
- 解散：仅 Owner；团队与全部成员关系一并软删除。
- 非成员访问团队任意接口：404（不泄露存在性）。

## 4. API 契约（Controller 仅转发，鉴权由框架 + Handler 判定）

| 方法 | 路由 | 说明 | 出参 |
|---|---|---|---|
| POST | `/api/team` | 创建 | `SimpleLong`（团队 id） |
| GET | `/api/team/list` | 我参与的团队（含 myRole/memberCount/avatar） | `QueryTeamsCommandResponse` |
| GET | `/api/team/{id}` | 详情（含 myRole） | `QueryTeamCommandResponse` |
| PUT | `/api/team/{id}` | 改名/简介 | Empty |
| DELETE | `/api/team/{id}` | 解散 | Empty |
| PUT | `/api/team/{id}/owner` | 转让所有权（原 Owner 降 Admin） | Empty |
| POST | `/api/team/{id}/avatar` | 设置头像（objectKey 须为已登记上传文件） | Empty |
| GET | `/api/team/{id}/users` | 成员列表 | `QueryTeamUsersCommandResponse` |
| POST | `/api/team/{id}/users` | 添加成员 `{userId, role}` | Empty |
| PUT | `/api/team/{id}/user/{userId}/role` | 改角色 `{role}` | Empty |
| DELETE | `/api/team/{id}/user/{userId}` | 移除/退出 | Empty |

路由回填 id 的 Command 不校验 id（Validate 只校验请求体字段，规避 SharpGrip 时序问题，见用户管理 SDD 决策）。

## 5. 关键决策

- **D1 软删除用 bool**：应管理层要求，team 模块 `IsDeleted` 为 bool（区别于既有表 long ticks）。兼容手段：实体显式实现 `IDeleteAudited.IsDeleted`（0/1 ↔ bool 转换），共享 `QueryFilter` 已扩展为按属性类型生成 `== false / == 0`（向后兼容）。防同名冲突改用 partial 唯一索引，删除/重建循环不受限。
- **D2 权限判定在 Handler**：团队角色依赖 team_user 表事实（区别于 usermanage 的 Controller 层 admin/root 门禁），Controller 仅转发。
- **D3 无物理外键**：与仓库约定一致，team_user.team_id/user_id 由应用层保证。
- **D4 long 序列化为字符串**：响应中 `teamId/userId` 为 JSON 字符串（全局 MVC 序列化配置），前端比较需 `Number()` 转换。

## 6. 已知问题 / 下阶段

- ~~所有权转让未实现~~ → 2026-09-02 二期已实现（`PUT /api/team/{id}/owner`，角色互换，见 TM-13）。
- ~~团队头像上传前端入口缺失~~ → 二期已实现（`POST /api/team/{id}/avatar` + 设置弹窗上传）。
- 知识库/插件挂载 team_id 在其各自模块落地时实现；配套「当前团队」上下文与侧边栏切换器随知识库模块一起交付。
