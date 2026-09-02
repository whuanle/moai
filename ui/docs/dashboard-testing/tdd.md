# 前端 Dashboard 与测试基建（Dashboard & Testing）验证映射（TDD）

> 关联：[SDD](./sdd.md) ｜ [BDD](./bdd.md)（场景定义） ｜ [TDD](./tdd.md) ｜ [SOP](./sop.md)
> 本文只做「场景 → 验证物 → 结果」映射。证据 = `cd ui && npm run test`（13 文件 42 用例）。

## 场景映射表

| 场景 | 验证物 | 结果（日期） |
|---|---|---|
| @FE-DT-S9 | `cd ui && npm run test`（vitest run，jsdom） | PASS 42/42（2026-09-01） |
| @FE-DT-S12、@FE-DT-S13 | [../../src/test/setup.ts](../../src/test/setup.ts) + 全量用例（匹配器与 matchMedia 桩被每个用例隐式依赖） | PASS 42/42（2026-09-01） |
| @FE-DT-S14 ~ @FE-DT-S17 | [../../src/pages/users/__tests__/Users.test.tsx](../../src/pages/users/__tests__/Users.test.tsx)（范式范本，3 用例） | PASS 3/3（2026-09-01） |
| @FE-DT-S1 ~ @FE-DT-S8、@FE-DT-S10、@FE-DT-S11 | @manual 浏览器走查/命令走查（[SOP 第 5 节](./sop.md)） | PASS（2026-09-01，见 SOP 历史验收存档） |

## 用例分布（13 文件 42 用例）

| 分组 | 文件（`ui/src/` 下 `__tests__`） | 用例 |
|---|---|---|
| theme | design-system/theme/{tokens,config}.test.ts | 6 |
| Feedback | design-system/components/Feedback/{feedback,bridge} | 15 |
| 通用组件 | Page/PageToolbar/QueryBar/DataTable/FormPage/DetailPage/Card(statCard)/Chat | 18 |
| 业务页 | pages/users/Users.test.tsx（唯一业务页测试） | 3 |

## 回归命令

```bash
cd ui && npm run test           # 一次性全量（CI 用）
cd ui && npm run test:watch     # watch 增量
cd ui && npm run typecheck
```

## 覆盖率说明

- Dashboard / DesignSystemPreview 无专属测试（静态展示页，走查验收）。
- 已知未覆盖：业务页测试仅 Users 一页（缺口清单见 [SDD 已知问题](./sdd.md)）；新增页面必须附 `__tests__` 并在本表补行（流程见 [SOP 第 2 节](./sop.md)）。
