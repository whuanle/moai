# 前端测试基建与设计样册（Testing & Preview）验证映射（TDD）

> 关联：[SDD](./sdd.md) ｜ [BDD](./bdd.md)（场景定义） ｜ [TDD](./tdd.md) ｜ [SOP](./sop.md)
> 本文只做「场景 → 验证物 → 结果」映射。证据 = `cd ui && npm run test`（65 文件 446 用例，2026-09-28）。

## 场景映射表

| 场景 | 验证物 | 结果（日期） |
|---|---|---|
| @FE-DT-S9 | `cd ui && npm run test`（vitest run，jsdom） | PASS 446/446（2026-09-28；2026-09-01 基线 42/42 见 SOP 存档） |
| @FE-DT-S12、@FE-DT-S13 | [../../src/test/setup.ts](../../src/test/setup.ts) + 全量用例（匹配器与 matchMedia 桩被每个用例隐式依赖） | PASS（2026-09-28） |
| @FE-DT-S14 ~ @FE-DT-S17 | [../../src/pages/users/__tests__/Users.test.tsx](../../src/pages/users/__tests__/Users.test.tsx)（范式范本）等业务页测试 | PASS（2026-09-28） |
| @FE-DT-S6 ~ @FE-DT-S8、@FE-DT-S10、@FE-DT-S11 | @manual 浏览器走查/命令走查（[SOP 第 5 节](./sop.md)） | PASS（2026-09-28，见 SOP 历史验收存档） |

> @FE-DT-S1~S5（概览页）已作废（2026-09-28），不再映射。

## 用例分布

当前规模 65 文件 446 用例（2026-09-28 全量本地运行），随迭代持续增长，明细以 `npm run test` 实际输出为准；2026-09-01 基线（13 文件 42 用例：theme 6 / Feedback 15 / 通用组件 18 / 业务页 3）见 [SOP 第 6 节](./sop.md)存档。

## 回归命令

```bash
cd ui && npm run test           # 一次性全量（CI 用）
cd ui && npm run test:watch     # watch 增量
cd ui && npm run typecheck
```

## 覆盖率说明

- DesignSystemPreview 无专属测试（静态展示页，走查验收）；原 Dashboard 页已下线（2026-09-28）。
- 已知未覆盖：以 `npm run test` 输出为准；新增页面必须附 `__tests__` 并在此登记（流程见 [SOP 第 2 节](./sop.md)）。
