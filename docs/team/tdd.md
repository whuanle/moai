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

### 2026-09-18 团队不可解散（@TM-S12/S13/S14）

- 需求变更：团队不可被解散，仅平台管理员可在「团队」界面禁用/启用团队。后端移除 `DissolveTeamCommand`/Handler 与 `DELETE /api/team/{id}` 端点；前端移除团队设置页「解散」按钮、`api/team.ts` 的 `dissolveTeam` 及相关 i18n key，Kiota 客户端已 `npm run syncapi` 重新生成。
- 后端构建：`dotnet build src/MoAI/MoAI.csproj` → **0 错误**（2026-09-18）。
- E2E：`node local-dev/team-e2e.mjs` → **PASS 68/68**（2026-09-18，真实 HTTP :5210）；TM-12 改为断言解散接口下线（405）且团队仍在，收尾清理改为管理员禁用归档；`node local-dev/gateway-e2e.mjs` → **23/23**（清理改为禁用）；kg/kg-external/wiki/wiki-external/wiki-doc 脚本的解散清理同步改为管理员禁用。
- 前端：`TeamManage.test.tsx` → **PASS 15/15**（Owner/非 Owner 设置页均无解散按钮）；`npm run typecheck`、`npm run lint` 全绿。

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
| 前端页面 | ui/src/pages/teams/__tests__/Teams.test.tsx | PASS 4/4（2026-09-02） |
| 浏览器走查 | @manual（登录 → 团队菜单 → 建团/成员管理） | PASS（2026-09-02） |

## 数据库专项证据

- partial 唯一索引：`idx_team_name_live_uindex … WHERE is_deleted = false`（psql \d 核对）
- 同名三轮"建→删→建→删"全部成功；存活重名插入被拒（psql 实测）
