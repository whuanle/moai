# 团队插件模块验证映射（TDD）

> 关联：[SDD](./sdd.md) ｜ [BDD](./bdd.md) ｜ [TDD](./tdd.md) ｜ [SOP](./sop.md) ｜ 证据：[local-dev/team-plugin-e2e.mjs](../../local-dev/team-plugin-e2e.mjs)

## 自检记录

- 构建：`dotnet build src/MoAI/MoAI.csproj` → 0 错误（2026-09-10）
- E2E：`TP_BASE=http://127.0.0.1:5000 node local-dev/team-plugin-e2e.mjs` → **19/19 PASS**（2026-09-10）
- 前端：`npm run typecheck && npm run lint && npm run test` → vitest 192/192、tsc、eslint 全绿（2026-09-10）
- 浏览器走查：团队页「插件」自定义/动态两 Tab、导入/编辑/运行/查看函数（待执行）

## 映射表

| 场景 | 验证物 | 结果（日期） |
|---|---|---|
| @TP-S1 | team-plugin-e2e.mjs（TP-01） | PASS（2026-09-10） |
| @TP-S2 / @TP-S3 | team-plugin-e2e.mjs（TP-02a-c、TP-03a-c） | 待执行 |
| @TP-S4 | team-plugin-e2e.mjs（TP-04a/b） | 待执行 |
| @TP-S5 | team-plugin-e2e.mjs（TP-05） | 待执行 |
| @TP-S6 | team-plugin-e2e.mjs（TP-06） | 待执行 |
| @TP-S7 | team-plugin-e2e.mjs（TP-07） | 待执行 |
| @TP-S8 | team-plugin-e2e.mjs（TP-08） | PASS（2026-09-10） |
| @TP-S9 | team-plugin-e2e.mjs（TP-09a/b、TP-19a/b） | PASS（2026-09-10） |
| @TP-S10 / @TP-S11 | team-plugin-e2e.mjs（TP-10b） | PASS（2026-09-10） |
| @TP-S12 / @TP-S13 / @TP-S15 | team-plugin-e2e.mjs（TP-12a、TP-13 跨团队、TP-26） | PASS（2026-09-10） |
| @TP-S16 / @TP-S17 / @TP-S18 | team-plugin-e2e.mjs（TP-16a/b、TP-17、TP-18） | 待执行 |
| @TP-S19 | team-plugin-e2e.mjs（TP-19a/b/c） | PASS（2026-09-10） |
| @TP-S20 | team-plugin-e2e.mjs（TP-20a/b） | PASS（2026-09-10） |
| @TP-S21 | team-plugin-e2e.mjs（TP-21、TP-21b） | PASS（2026-09-10） |
| @TP-S22 | team-plugin-e2e.mjs（TP-22） | PASS（2026-09-10） |
| @TP-S23 | team-plugin-e2e.mjs（TP-23） | PASS（2026-09-10） |
| @TP-S24 | team-plugin-e2e.mjs（TP-24） | PASS（2026-09-10） |
| @TP-S25 | team-plugin-e2e.mjs（TP-25、TP-25b） | PASS（2026-09-10） |
| @TP-S26 | team-plugin-e2e.mjs（TP-26） | PASS（2026-09-10） |
| @TP-S27 | team-plugin-e2e.mjs（TP-13 跨团队） | PASS（2026-09-10） |
| @TP-S29 | team-plugin-e2e.mjs（TP-29a/b/c：MCP 桩校验插值后明文） | PASS（2026-09-20） |
| @TP-S30 | team-plugin-e2e.mjs（TP-30a/b、TP-31：detail 回显占位符 + 刷新插值） | PASS（2026-09-20） |
| @TP-S31 | team-plugin-e2e.mjs（TP-32：缺失变量 409） | PASS（2026-09-20） |
| @TP-S32 | team-plugin-e2e.mjs（TP-33a/b/c、TP-34a/b：OpenAPI 保存回显）+ 单测 CustomPluginVariableInterpolatorTests | PASS（2026-09-20） |
| @TP-S28 | ui/src/pages/teams/plugins/__tests__/TeamPlugins.test.tsx | PASS（2026-09-10，两 Tab/管理入口） |
| 前端：团队插件页 | ui/src/pages/teams/__tests__/TeamManage.test.tsx | PASS（2026-09-10，插件区块嵌入） |
| 前端：系统插件授权抽屉 | ui/src/pages/plugins/__tests__/CustomPluginPanel.test.tsx（回归） | PASS（2026-09-10） |
| 浏览器走查 | @manual（见 sop.md 第 4 节） | 待执行 |

