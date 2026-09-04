# 变量管理模块验证映射（TDD）

> 关联：[SDD](./sdd.md) ｜ [BDD](./bdd.md) ｜ [TDD](./tdd.md) ｜ [SOP](./sop.md) ｜ 证据：[local-dev/variable-e2e.mjs](../../local-dev/variable-e2e.mjs)

## 自检记录

- 构建：`dotnet build src/variable/MoAI.Variable.Core/MoAI.Variable.Core.csproj` → 0 错误（2026-09-04）
- E2E：`node local-dev/variable-e2e.mjs` → 待重跑（2026-09-04，后端需重启加载新契约）
- 前端：vitest **72/72**（Variables + TeamManage 等）+ tsc + eslint 全绿（2026-09-04）
- 浏览器走查：切换器选团队 → 建普通/私密变量 → 私密值列表掩码 → 编辑私密变量留空/填值（截图存档）

## 映射表

| 场景 | 验证物 | 结果（日期） |
|---|---|---|
| @VR-S1 | variable-e2e.mjs（VR-01） | PASS（2026-09-03） |
| @VR-S2 | variable-e2e.mjs（VR-02a-c） | PASS（2026-09-03） |
| @VR-S3 | variable-e2e.mjs（VR-03a/b） | PASS（2026-09-03） |
| @VR-S4 / @VR-S5 | variable-e2e.mjs（VR-04a/b、VR-05） | PASS（2026-09-03） |
| @VR-S6 / @VR-S7 | variable-e2e.mjs（VR-06a-c、VR-07a/b、VR-08a/b） | 契约变更待重跑（2026-09-04） |
| @VR-S9 | variable-e2e.mjs（VR-09a-g） | 契约变更待重跑（2026-09-04） |
| @VR-S10 | variable-e2e.mjs（VR-10a/b） | 契约变更待重跑（2026-09-04） |
| @VR-S11 | variable-e2e.mjs（VR-11） | 契约变更待重跑（2026-09-04） |
| @VR-S12 | variable-e2e.mjs（VR-12a-c） | PASS（2026-09-03） |
| 前端页面 | ui/src/pages/variables/__tests__/Variables.test.tsx | PASS 4/4（2026-09-03） |
| 浏览器走查 | @manual（建普通/私密变量、掩码、编辑私密留空/填值、key 可改） | PASS（2026-09-03，截图存档） |
