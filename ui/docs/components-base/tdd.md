# 前端基础组件（components-base）验证映射（TDD）

> 关联：[SDD](./sdd.md) ｜ [BDD](./bdd.md)（场景定义） ｜ [TDD](./tdd.md) ｜ [SOP](./sop.md)
> 本文只做「场景 → 验证物 → 结果」映射；场景语义见 BDD 编号，设计依据见 SDD。

## 场景映射表

| 场景 | 验证物 | 结果（日期） |
|---|---|---|
| @FE-CB-S1、@FE-CB-S2 | [Page/__tests__/index.test.tsx](../../src/design-system/components/Page/__tests__/index.test.tsx) | PASS 2/2（2026-09-01） |
| @FE-CB-S6 | [Card/__tests__/statCard.test.tsx](../../src/design-system/components/Card/__tests__/statCard.test.tsx) | PASS 1/1（2026-09-01） |
| @FE-CB-S9 ~ @FE-CB-S12 | [DataTable/__tests__/index.test.tsx](../../src/design-system/components/DataTable/__tests__/index.test.tsx) | PASS 4/4（2026-09-01） |
| @FE-CB-S14、@FE-CB-S15 | [QueryBar/__tests__/index.test.tsx](../../src/design-system/components/QueryBar/__tests__/index.test.tsx) | PASS 2/2（2026-09-01） |
| @FE-CB-S17、@FE-CB-S18 | [PageToolbar/__tests__/index.test.tsx](../../src/design-system/components/PageToolbar/__tests__/index.test.tsx) | PASS 2/2（2026-09-01） |
| @FE-CB-S3 ~ @FE-CB-S5、@FE-CB-S7、@FE-CB-S8、@FE-CB-S13、@FE-CB-S16 | @manual 浏览器/代码走查（[SOP 第 5 节](./sop.md)；样式与布局断言未单测化） | PASS（2026-09-01，见 SOP 存档） |

## 回归命令

```bash
cd ui && npx vitest run src/design-system/components/Page src/design-system/components/Card \
  src/design-system/components/DataTable src/design-system/components/QueryBar \
  src/design-system/components/PageToolbar        # 定向：5 files / 11 tests
cd ui && npm run test -- --run                    # 全量基线：13 files / 42 tests
cd ui && npm run lint && npm run typecheck
```

## 覆盖率说明

- 交互回调类 11 个场景已自动化（11 用例）；纯样式/布局类（Page 无头部、Card 视觉、趋势角标、骨架、分页默认值、受控实例）7 个场景为走查型。
- jsdom 输出的 `Not implemented: Window's getComputedStyle ... with pseudo-elements` 为环境噪声，非失败。
