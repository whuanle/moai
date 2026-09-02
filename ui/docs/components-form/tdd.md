# 前端表单与反馈组件（components-form）验证映射（TDD）

> 关联：[SDD](./sdd.md) ｜ [BDD](./bdd.md)（场景定义） ｜ [TDD](./tdd.md) ｜ [SOP](./sop.md)
> 本文只做「场景 → 验证物 → 结果」映射；场景语义见 BDD 编号，设计依据见 SDD。

## 场景映射表

| 场景 | 验证物 | 结果（日期） |
|---|---|---|
| @FE-CF-S1 ~ @FE-CF-S3 | [FormPage/__tests__/index.test.tsx](../../src/design-system/components/FormPage/__tests__/index.test.tsx) | PASS 3/3（2026-09-01） |
| @FE-CF-S4 ~ @FE-CF-S6 | [DetailPage/__tests__/index.test.tsx](../../src/design-system/components/DetailPage/__tests__/index.test.tsx) | PASS 3/3（2026-09-01） |
| @FE-CF-S7 | [Chat/__tests__/index.test.tsx](../../src/design-system/components/Chat/__tests__/index.test.tsx) | PASS 1/1（2026-09-01） |
| @FE-CF-S10 | [Feedback/__tests__/bridge.test.tsx](../../src/design-system/components/Feedback/__tests__/bridge.test.tsx) | PASS 1/1（2026-09-01） |
| @FE-CF-S11 ~ @FE-CF-S14 | [Feedback/__tests__/feedback.test.ts](../../src/design-system/components/Feedback/__tests__/feedback.test.ts)（错误分类 5 + 响应体解析 3 + 路由 6） | PASS 14/14（2026-09-01） |
| @FE-CF-S8、@FE-CF-S9 | @manual 走查（空态文案/未注册降级，[SOP 第 5 节](./sop.md)） | PASS（2026-09-01，见 SOP 存档） |

## 回归命令

```bash
cd ui && npx vitest run src/design-system/components/FormPage src/design-system/components/DetailPage \
  src/design-system/components/Chat src/design-system/components/Feedback   # 定向：5 files / 22 tests
cd ui && npm run test -- --run           # 全量基线：13 files / 42 tests
cd ui && npm run lint && npm run typecheck
```

## 覆盖率说明

- 12/14 场景自动化（22 用例，Feedback 占 15）；空态与未注册降级 2 个场景为走查型。
- 布局常量曾以 grep 复核：FormPage maxWidth 720（FormPage.tsx:29）、Chat height 480（Chat.tsx:28）、Feedback/index.ts 6 组导出。
- jsdom 的 `getComputedStyle ... pseudo-elements` 输出为环境噪声，非失败。
