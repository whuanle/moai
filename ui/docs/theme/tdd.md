# 前端主题系统（theme）验证映射（TDD）

> 关联：[SDD](./sdd.md) ｜ [BDD](./bdd.md)（场景定义） ｜ [TDD](./tdd.md) ｜ [SOP](./sop.md)
> 本文只做「场景 → 验证物 → 结果」映射；场景语义见 BDD 编号，设计依据见 SDD。

## 场景映射表

| 场景 | 验证物 | 结果（日期） |
|---|---|---|
| @FE-TH-S1、@FE-TH-S2 | [config.test.ts](../../src/design-system/theme/__tests__/config.test.ts)（3 用例：默认预设/两套预设/类型收口） | PASS 3/3（2026-09-01） |
| @FE-TH-S7 | [tokens.test.ts](../../src/design-system/theme/__tests__/tokens.test.ts)（3 用例：主色/间距栅格/圆角字体） | PASS 3/3（2026-09-01） |
| @FE-TH-S3、@FE-TH-S4 | @manual 代码走查：store/app.ts `getInitialTheme`（localStorage → matchMedia → 'light'） | PASS（2026-09-01） |
| @FE-TH-S5 | @manual 浏览器走查：Sider Select 切换 + 刷新保持（[SOP 第 4 节](./sop.md)） | PASS（2026-09-01） |
| @FE-TH-S6、@FE-TH-S9 | @manual 装配走查：AppProviders（ConfigProvider/antd App/FeedbackBridge、useEffect([locale])） | PASS（2026-09-01） |
| @FE-TH-S8 | @manual 代码走查：业务组件样式值取自 `@/design-system` 令牌 | PASS（2026-09-01） |

## 回归命令

```bash
cd ui && npx vitest run src/design-system/theme    # 定向：2 files / 6 tests
cd ui && npm run typecheck && npm run lint         # 佐证
```

## 覆盖率说明

- 预设与令牌 3 个场景自动化（6 用例）；初始化/切换/装配 6 个场景为走查型（zustand store 与 AppProviders 无组件单测）。
- 防漂移：主色 #2970FF 同时被 config/tokens 两组测试锁定（旧文档 #4A9EFF 已过时，见 [SDD 已知问题](./sdd.md)）。
