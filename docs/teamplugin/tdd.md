# 团队插件模块验证映射（TDD）

> 关联：[SDD](./sdd.md) ｜ [BDD](./bdd.md) ｜ [TDD](./tdd.md) ｜ [SOP](./sop.md) ｜ 证据：[local-dev/team-plugin-e2e.mjs](../../local-dev/team-plugin-e2e.mjs)

## 自检记录

- 构建：`dotnet build src/teamplugin/MoAI.TeamPlugin.Core/MoAI.TeamPlugin.Core.csproj` → 0 错误（2026-09-07）
- 构建：`dotnet build src/database/MoAI.Database.Postgres/MoAI.Database.Postgres.csproj` → 0 错误（2026-09-07）
- 构建：`dotnet build src/aiplugin/MoAI.AIPlugin.Custom/MoAI.AIPlugin.Custom.csproj` → 0 错误（2026-09-07）
- E2E：`node local-dev/team-plugin-e2e.mjs` → 待执行（后端需运行于 5210 且建表）
- 前端：vitest **99/99**（25 文件）+ tsc + eslint 全绿（2026-09-07）
- 浏览器走查：系统插件抽屉授权团队 → 团队页「插件」区块创建/删除团队插件（待执行）

## 映射表

| 场景 | 验证物 | 结果（日期） |
|---|---|---|
| @TP-S1 | team-plugin-e2e.mjs（TP-01） | 待执行 |
| @TP-S2 / @TP-S3 | team-plugin-e2e.mjs（TP-02a-c、TP-03a-c） | 待执行 |
| @TP-S4 | team-plugin-e2e.mjs（TP-04a/b） | 待执行 |
| @TP-S5 | team-plugin-e2e.mjs（TP-05） | 待执行 |
| @TP-S6 | team-plugin-e2e.mjs（TP-06） | 待执行 |
| @TP-S7 | team-plugin-e2e.mjs（TP-07） | 待执行 |
| @TP-S8 | team-plugin-e2e.mjs（TP-08） | 待执行 |
| @TP-S9 | team-plugin-e2e.mjs（TP-09a/b） | 待执行 |
| @TP-S10 / @TP-S11 | team-plugin-e2e.mjs（TP-10a/b、TP-11） | 待执行 |
| @TP-S12 / @TP-S13 / @TP-S14 / @TP-S15 | team-plugin-e2e.mjs（TP-12a/b、TP-13、TP-14、TP-15） | 待执行 |
| @TP-S16 / @TP-S17 / @TP-S18 | team-plugin-e2e.mjs（TP-16a/b、TP-17、TP-18） | 待执行 |
| @TP-S19 | team-plugin-e2e.mjs（TP-19a/b） | 待执行 |
| 前端：团队插件页 | ui/src/pages/teams/__tests__/TeamManage.test.tsx | PASS（2026-09-07，插件区块嵌入） |
| 前端：系统插件授权抽屉 | ui/src/pages/plugins/__tests__/CustomPluginPanel.test.tsx（回归） | PASS（2026-09-07） |
| 浏览器走查 | @manual（见 sop.md 第 4 节） | 待执行 |
