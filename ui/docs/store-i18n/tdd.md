# 前端状态管理与 i18n（Store & i18n）验证映射（TDD）

> 关联：[SDD](./sdd.md) ｜ [BDD](./bdd.md)（场景定义） ｜ [TDD](./tdd.md) ｜ [SOP](./sop.md)
> 本文只做「场景 → 验证物 → 结果」映射；场景语义见 BDD 编号，设计依据见 SDD。证据 = vitest + typecheck。

## 场景映射表

| 场景 | 验证物 | 结果（日期） |
|---|---|---|
| @FE-SI-S4 | [../../src/design-system/theme/__tests__/config.test.ts](../../src/design-system/theme/__tests__/config.test.ts)（3 用例）+ [tokens.test.ts](../../src/design-system/theme/__tests__/tokens.test.ts)（3 用例） | PASS 6/6（2026-09-01） |
| @FE-SI-S17、@FE-SI-S18 | [../../src/pages/users/__tests__/Users.test.tsx](../../src/pages/users/__tests__/Users.test.tsx)（store 注入 + 角色差异化渲染，3 用例，间接覆盖） | PASS 3/3（2026-09-01） |
| @FE-SI-S1 ~ @FE-SI-S3、@FE-SI-S5 ~ @FE-SI-S16 | @manual 浏览器走查（[SOP 第 5 节](./sop.md)） | PASS（2026-09-01，见 SOP 历史验收存档） |
| 本模块全部源码类型契约 | `cd ui && npm run typecheck`（tsc -b --noEmit） | PASS 0 错误（2026-09-01） |

## 回归命令

```bash
cd ui && npm run test        # vitest 全量：本模块相关用例含于 13 文件 42 用例
cd ui && npm run typecheck
```

## 覆盖率说明

- 本模块无专属 `__tests__`；自动化证据 = 依赖本模块的既有 vitest（主题双预设配置 + Users 页 store 注入约定）+ typecheck 0 错误。
- 已知未覆盖：persist 水合时序、缺失词条回退的实际渲染（均为走查项，见 [SOP 第 5 节](./sop.md)）。
