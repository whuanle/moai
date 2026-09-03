# 变量页（/variable）验证映射（TDD）

> 关联：[SDD](./sdd.md) ｜ [BDD](./bdd.md) ｜ [TDD](./tdd.md) ｜ [SOP](./sop.md) ｜ 证据：`ui/src/pages/variables/__tests__/Variables.test.tsx`

## 映射表

| 场景 | 验证物 | 结果（日期） |
|---|---|---|
| @FE-VR-S1 | Variables.test.tsx「渲染变量列表：普通变量显示值、私密变量掩码」 | PASS 4/4（2026-09-03） |
| @FE-VR-S2 | Variables.test.tsx「Admin 角色显示新建与操作列」 | PASS（2026-09-03） |
| @FE-VR-S3 | Variables.test.tsx「Member 角色只读」 | PASS（2026-09-03） |
| @FE-VR-S4 | Variables.test.tsx「未选择团队时提示先选团队」 | PASS（2026-09-03） |
| @FE-VR-S5 | @manual（浏览器：编辑私密留空保存 → 替换接口验证值不变） | PASS（2026-09-03） |
| 后端契约 | local-dev/variable-e2e.mjs | PASS 26/26（2026-09-03） |

## 回归门禁

`cd ui && npm run test`（全量 56/56）+ `npm run typecheck` + `npm run lint` 全绿（2026-09-03）。
