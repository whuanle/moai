# 团队模块验证映射（TDD）

> 关联：[SDD](./sdd.md) ｜ [BDD](./bdd.md) ｜ [TDD](./tdd.md) ｜ [SOP](./sop.md) ｜ 证据：[local-dev/team-e2e.mjs](../../local-dev/team-e2e.mjs)

## 自检记录

- 构建：`dotnet build src/MoAI/MoAI.csproj` → 0 错误（2026-09-02）
- E2E：`node local-dev/team-e2e.mjs` → **PASS 47/47**（2026-09-02 二期含 TM-13/14，真实 HTTP :5210）
- 数据库：`asserts/team.sql` 已执行，`team`/`team_user` 落库；partial 唯一索引三轮"删除→重建"冒烟通过
- 前端：`npm run test` 46/46（Teams 4）、tsc、eslint 全绿（2026-09-02）

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
| @TM-S12 | team-e2e.mjs（TM-12a-d） | PASS（2026-09-02） |
| @TM-S13 | team-e2e.mjs（TM-13a-f） | PASS（2026-09-02） |
| @TM-S14 | team-e2e.mjs（TM-14a-f，含存储直传全链路） | PASS（2026-09-02） |
| 前端页面 | ui/src/pages/teams/__tests__/Teams.test.tsx | PASS 4/4（2026-09-02） |
| 浏览器走查 | @manual（登录 → 团队菜单 → 建团/成员管理） | PASS（2026-09-02） |

## 数据库专项证据

- partial 唯一索引：`idx_team_name_live_uindex … WHERE is_deleted = false`（psql \d 核对）
- 同名三轮"建→删→建→删"全部成功；存活重名插入被拒（psql 实测）
