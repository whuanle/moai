# 知识库模块验证映射（TDD）

> 关联：[SDD](./sdd.md) ｜ [BDD](./bdd.md) ｜ [TDD](./tdd.md) ｜ [SOP](./sop.md) ｜ 证据：[local-dev/wiki-e2e.mjs](../../local-dev/wiki-e2e.mjs)

## 自检记录

- 构建：`dotnet build src/MoAI/MoAI.csproj` → 0 错误（2026-09-02）
- E2E：`node local-dev/wiki-e2e.mjs` → **PASS 23/23**（2026-09-02，真实 HTTP :5210）
- E2E：`node local-dev/wiki-doc-e2e.mjs` → **PASS 15/15**（2026-09-03 二期文档层）
- 前端：vitest **52/52**（Wiki 4 + 文档页 2）+ tsc + eslint 全绿（2026-09-03）
- 浏览器走查：侧边栏切换器选择团队 → /wiki 建库 → Admin 操作列渲染（2026-09-02）

## 映射表

| 场景 | 验证物 | 结果（日期） |
|---|---|---|
| @WK-S1 | wiki-e2e.mjs（WK-01） | PASS（2026-09-02） |
| @WK-S2 | wiki-e2e.mjs（WK-02a-c） | PASS（2026-09-02） |
| @WK-S3 | wiki-e2e.mjs（WK-03a/b） | PASS（2026-09-02） |
| @WK-S4 | wiki-e2e.mjs（WK-04a-c） | PASS（2026-09-02） |
| @WK-S5 | wiki-e2e.mjs（WK-05a/b） | PASS（2026-09-02） |
| @WK-S6 | wiki-e2e.mjs（WK-06a-c） | PASS（2026-09-02） |
| @WK-S7 | wiki-e2e.mjs（WK-07a-d） | PASS（2026-09-02） |
| @WK-S8 | wiki-e2e.mjs（WK-08a-e） | PASS（2026-09-02） |
| 前端页面 | ui/src/pages/wiki/__tests__/Wiki.test.tsx | PASS 4/4（2026-09-02） |
| @WD-S1…@WD-S8 | local-dev/wiki-doc-e2e.mjs（WD-01…WD-08） | PASS 15/15（2026-09-03） |
| 前端文档页 | ui/src/pages/wiki/__tests__/WikiDocuments.test.tsx | PASS 2/2（2026-09-03） |
| 浏览器走查 | @manual（切换器 → 建库 → 权限渲染） | PASS（2026-09-02） |
| 浏览器走查 | @manual（建文档 → 编辑器回显） | PASS（2026-09-03） |
