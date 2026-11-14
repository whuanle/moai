# 团队模块验证映射（TDD）

> 关联：[SDD](./sdd.md) ｜ [BDD](./bdd.md) ｜ [TDD](./tdd.md) ｜ [SOP](./sop.md) ｜ 证据：[local-dev/team-e2e.mjs](../../local-dev/team-e2e.mjs)

## 自检记录

- 构建：`dotnet build src/MoAI/MoAI.csproj` → 0 错误（2026-09-02）
- E2E：`node local-dev/team-e2e.mjs` → **PASS 47/47**（2026-09-02 二期含 TM-13/14，真实 HTTP :5210）
- 数据库：`asserts/team.sql` 已执行，`team`/`team_user` 落库；partial 唯一索引三轮"删除→重建"冒烟通过
- 前端：`npm run test` 46/46（Teams 4）、tsc、eslint 全绿（2026-09-02）

### 2026-09-10 管理员团队治理（@TM-S15/S15b/S16）

- 后端构建：**未执行**。本机 CLI 环境被 NuGet `ConfigurationDefaults` 阻断（`Value cannot be null (Parameter 'path1')`，与轮次 30/31/33 记录同类），`dotnet build --no-restore` 亦受工程资产文件重算影响，无法产出编译结论；**须在系统终端先 `dotnet restore` 再 `dotnet build src/MoAI/MoAI.csproj` → 0 错误**（`MoAI.Team.Core.csproj` 按规范新增了 `MoAI.Account.Shared` 引用以使用 `IUserInfoFillService`，故必须重新还原）。
- E2E：`node local-dev/team-e2e.mjs`（TM-15a~m、TM-16a~h 已补入脚本，含种子 `admin` 登录）**未执行**，需后端运行于 :5210 后跑，预期 **68/68**。
- 前端：`ui/src/pages/admin/__tests__/AdminTeams.test.tsx` → **PASS 3/3**（2026-09-10）；`eslint` 0 error；`tsc` 除 `src/api/team.ts` 三处 `client.api.admin` 未定义（待 `npm run syncapi` 同步 Kiota 客户端后消除，属后端不可运行导致的流程中断）外无其它错误。

### 2026-09-17 团队详情页导航收敛（@TM-S17）

- 前端：`ui/src/pages/teams/__tests__/TeamManage.test.tsx` → **PASS 15/15**（2026-09-17）；团队详情页面包屑改为「主页 / 团队头像+团队名称」，移除「团队信息」菜单与信息卡片，默认分区与越权/非法子路由回落分区均改为「内部应用」。

### 2026-11-03 团队侧边栏头部收敛（@TM-S17）

- 前端：`ui/src/pages/teams/TeamManage.tsx` 移除 `Page` 面包屑（「主页 / 团队名」），右侧内容区不再预留面包屑空白高度；侧边栏头部改为「团队头像(30px)+团队名称（ellipsis）+ 返回主页」按钮（→ /apps），其下再渲染分区菜单；i18n 新增 `team.backHome`（zh-CN/en-US）。测试断言同步（无「主页」面包屑、有「返回主页」）→ **PASS 18/18**（2026-11-03）；`npm run typecheck` 0 错误、`npm run lint` 0 error（无新增警告）。
- 前端：菜单分组隔离——原 13 项平铺菜单拆为「成员可用分区」（apps/prompts/skills/knowledge/knowledgegraph）与「Owner/Admin 管理分区」（externalApps 及以下）两段，Owner/Admin 视图在两段之间渲染 antd Menu divider + `type:'group'` 分组标题「管理」（i18n `team.menuGroupManage`），普通成员仍平铺使用分区不渲染分组；新增用例断言 Owner 有且仅有一个「管理」分组标题、成员无分组标题 → **PASS 19/19**（2026-11-03）。

### 2026-11-07 成员分区布局整改（@TM-S17）

- 前端：`TeamManage.tsx` 成员分区去掉外层 `DSCard` 大卡片容器（`DataTable` 自带白色卡片容器，外包即双卡片，对齐使用分区 Dify 式分层口径）；手写 flex 工具行（`Input.Search` 实时过滤 + 独立「刷新」按钮）改设计系统 `QueryBar`（搜索框 → 查询 → | → 「添加成员」按钮在 `extra`），查询按钮自带刷新语义故移除 `DataTable` 的 `onRefresh`，关键字过滤改查询提交时生效（对齐组织用户页 UsersPanel 口径）→ **PASS 19/19**（2026-11-07）；`npm run typecheck` 0 错误、`npm run lint` 0 error。

### 2026-11-07 成员列表联系方式脱敏展示（@TM-S17）

- 需求：成员表格参考系统用户管理页补列（邮箱/手机号），但团队侧不得泄露完整联系方式——脱敏在服务端完成，明文不出库。
- 后端：`TeamUserItem` 新增 `Email`/`Phone` 字段（XML 注释声明已脱敏），`QueryTeamUsersCommandHandler` 内存投影层脱敏（邮箱本地部分保留首字符如 `z***@example.com`；手机号 ≥8 位保留前 3 后 4 如 `138****5678`）；`dotnet build src/MoAI/MoAI.csproj -o .builds/member-mask` → **0 错误**（2026-11-07）；Kiota 客户端经独立端口临时实例 `npm run syncapi -- http://127.0.0.1:5010/openapi/v1.json` 重生成（`/openapi/v1.json` TeamUserItem 含 `email`/`phone`）。
- 接口冒烟：临时实例（5010）admin 登录 → `GET /api/team/1/users` → 实际返回 `email=1***@qq.com/135****7652` 形态，明文未出服务端（2026-11-07）。
- 前端：`TeamManage.tsx` 成员表格新增「邮箱/手机号」列（脱敏值直显、空值 `-`），成员列头像接 `resolveStorageUrl(record.avatar)` 并对齐 UsersPanel 36px 规格；i18n 新增 `team.colEmail`/`team.colPhone`（zh-CN/en-US 同步）；`TeamManage.test.tsx` → **PASS 19/19**（2026-11-07）；`npm run typecheck` 0 错误、`eslint` 0 告警。

### 2026-09-18 团队不可解散（@TM-S12/S13/S14）

- 需求变更：团队不可被解散，仅平台管理员可在「团队」界面禁用/启用团队。后端移除 `DissolveTeamCommand`/Handler 与 `DELETE /api/team/{id}` 端点；前端移除团队设置页「解散」按钮、`api/team.ts` 的 `dissolveTeam` 及相关 i18n key，Kiota 客户端已 `npm run syncapi` 重新生成。
- 后端构建：`dotnet build src/MoAI/MoAI.csproj` → **0 错误**（2026-09-18）。
- E2E：`node local-dev/team-e2e.mjs` → **PASS 68/68**（2026-09-18，真实 HTTP :5210）；TM-12 改为断言解散接口下线（405）且团队仍在，收尾清理改为管理员禁用归档；`node local-dev/gateway-e2e.mjs` → **23/23**（清理改为禁用）；kg/kg-external/wiki/wiki-external/wiki-doc 脚本的解散清理同步改为管理员禁用。
- 前端：`TeamManage.test.tsx` → **PASS 15/15**（Owner/非 Owner 设置页均无解散按钮）；`npm run typecheck`、`npm run lint` 全绿。

### 2026-10-29 添加成员弹窗：组织架构批量选人 + 全站实时搜索（@TM-S19/S20/S21）

- 需求：成员管理移除内联「搜索并选择用户」下拉，改为点「添加成员」弹模态窗——左侧组织架构树按组织批量选人，关键字实时搜索全站用户。
- 后端：新增 `POST /api/team/{id}/users/batch`（`AddTeamUsersCommand`，整批校验非空/去重/≤200，跳过已入团与不存在用户并回传明细）与团队侧只读组织视图 `GET /api/team/{id}/org/role/list`、`GET /api/team/{id}/org/role/{roleId}/users`（Team.Api 引用 `MoAI.Org.Shared` 经 MediatR 复用组织查询，门禁在 TeamController）；Kiota 客户端已 `npm run syncapi` 重新生成。
- 后端构建：`dotnet build src/MoAI/MoAI.csproj` → **0 错误**（2026-10-29，独立输出目录构建，:5000 存量实例锁 bin 不影响编译结论）。
- E2E：`node local-dev/team-e2e.mjs` → **PASS 85/85**（2026-10-29，真实 HTTP :5210，新增 TM-18a~i 批量添加 + TM-17a~h 组织视图）。
- 前端：成员工具栏改「添加成员」按钮 + 新组件 `TeamMemberAddModal`（组织树/多选/全选/已选标签移除/300ms 防抖搜索/已是成员禁选）；i18n `team.*` zh/en 同步（新增 10 键，移除孤儿键 `addMemberPlaceholder`/`addSuccess`）；`TeamManage.test.tsx` → **PASS 18/18**；`npm run typecheck`、`npm run lint` 全绿。

## 映射表

| 场景 | 验证物 | 结果（日期） |
|---|---|---|
| @TM-S1 | team-e2e.mjs（TM-01） | PASS（2026-09-02） |
| @TM-S2 | team-e2e.mjs（TM-02/TM-05） | PASS（2026-09-02） |
| @TM-S3 | team-e2e.mjs（TM-03） | PASS（2026-09-02） |
| @TM-S4 / @TM-S4b | team-e2e.mjs（TM-04a/b/c） | PASS（2026-09-02） |
| @TM-S5 / @TM-S5b | team-e2e.mjs（TM-06a/b/c） | PASS（2026-09-02） |
| @TM-S7 / @TM-S7b | team-e2e.mjs（TM-07a-f） | PASS（2026-09-02） |
| @TM-S8 | team-e2e.mjs（TM-08a-d） | PASS（2026-09-02） |
| @TM-S9 | team-e2e.mjs（TM-09） | PASS（2026-09-02） |
| @TM-S10 全部 | team-e2e.mjs（TM-10a-g） | PASS（2026-09-02） |
| @TM-S11 | team-e2e.mjs（TM-11a-c） | PASS（2026-09-02） |
| @TM-S12 | team-e2e.mjs（TM-12a/b：解散接口下线 405、团队仍在） | PASS（2026-09-18） |
| @TM-S13 | team-e2e.mjs（TM-13a-f：13f 改断言解散接口对任意角色均 405） | PASS（2026-09-18） |
| @TM-S14 | team-e2e.mjs（TM-14a-e，含存储直传全链路；14f 解散清理随能力移除删除） | PASS（2026-09-18） |
| @TM-S15 | team-e2e.mjs（TM-15a-f：鉴权/列表/负责人/成员数） | PASS 68/68 全量（2026-09-18） |
| @TM-S15b | team-e2e.mjs（TM-15g-m：禁用/启用/状态筛选） | PASS 68/68 全量（2026-09-18） |
| @TM-S16 | team-e2e.mjs（TM-16a-h：转让非成员自动入团+角色降级；16h 清理改管理员禁用） | PASS 68/68 全量（2026-09-18） |
| @TM-S15 / @TM-S15b / @TM-S16（前端） | ui/src/pages/admin/__tests__/AdminTeams.test.tsx | PASS 3/3（2026-09-10） |
| @TM-S16b | @manual（代码走查：两个列表 Handler 的 Owner 字典改 GroupBy 首条容错；MoAI.Team.Core 构建 0 错误） | PASS（2026-09-14） |
| @TM-S17 | ui/src/pages/teams/__tests__/TeamManage.test.tsx | PASS 15/15（2026-09-17） |
| @TM-S18 | ui/src/pages/teams/__tests__/TeamManage.test.tsx（成员列昵称主行断言） | PASS 15/15（2026-10-29） |
| @TM-S19 | team-e2e.mjs（TM-18a-i：批量校验/权限/跳过明细/成员列表） | PASS 85/85（2026-10-29） |
| @TM-S20 | team-e2e.mjs（TM-17a-h：401/404/403 门禁与组织数据） | PASS 85/85（2026-10-29） |
| @TM-S21 | ui/src/pages/teams/__tests__/TeamManage.test.tsx（弹窗组织选人/已是成员禁选/实时搜索） | PASS 18/18（2026-10-29） |
| 前端页面 | ui/src/pages/teams/__tests__/Teams.test.tsx | PASS 4/4（2026-09-02） |
| 浏览器走查 | @manual（登录 → 团队菜单 → 建团/成员管理） | PASS（2026-09-02） |

## 数据库专项证据

- partial 唯一索引：`idx_team_name_live_uindex … WHERE is_deleted = false`（psql \d 核对）
- 同名三轮"建→删→建→删"全部成功；存活重名插入被拒（psql 实测）
