# 用户管理（User Management）验证映射（TDD）

> 关联：[SDD](./sdd.md) ｜ [BDD](./bdd.md)（场景定义） ｜ [TDD](./tdd.md) ｜ [SOP](./sop.md)
> 本文只做「场景 → 验证物 → 结果」映射；场景语义见 BDD 编号，设计依据见 SDD。

## 场景映射表

| 场景 | 验证物 | 结果（日期） |
|---|---|---|
| @UM-S1 ~ @UM-S21（全部后端场景） | [local-dev/user-management-e2e.mjs](../../local-dev/user-management-e2e.mjs)（真实 HTTP，34 断言） | PASS 34/34（2026-09-02） |
| @UM-S22、@UM-S23 | [ui/src/pages/users/__tests__/Users.test.tsx](../../ui/src/pages/users/__tests__/Users.test.tsx)（3 用例） | PASS 3/3（2026-09-02） |
| @UM-S24 ~ @UM-S26 | @manual 浏览器走查（[SOP 第 5 节](./sop.md)） | PASS（2026-09-02，见 SOP 验收记录） |

## 回归命令

```bash
node local-dev/user-management-e2e.mjs     # 需后端运行于 :5210
cd ui && npx vitest run src/pages/users
```

## 覆盖率说明

- 后端 21 个场景全部自动化；前端 3 个 vitest + 3 个手动（弹窗校验/确认框为 UI 交互细节）。
- 已知未覆盖：并发禁用竞态（低风险，缓存失效后必拦）。
