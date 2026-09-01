# 前端 Dashboard 与测试基建（Dashboard & Testing）操作手册（SOP）

> 关联：[SDD](./sdd.md) ｜ [BDD](./bdd.md)（验收场景编号） ｜ [TDD](./tdd.md)（自动化映射） ｜ [SOP](./sop.md)

## 1. 运行与解读

```bash
cd ui
npm run test           # vitest run 一次性全量（CI 用）→ [@FE-DT-S9](./bdd.md#fe-dt-s9)
npm run test:watch     # watch 增量重跑 → [@FE-DT-S10](./bdd.md#fe-dt-s10)
npm run typecheck      # tsc -b --noEmit（含测试文件类型）→ [@FE-DT-S11](./bdd.md#fe-dt-s11)
npm run lint
```

- 测试配置内联于 `vite.config.ts` 的 `test` 段（**无独立 vitest.config**）。
- 输出中 `Not implemented: Window's getComputedStyle() ... pseudo-elements` 为 jsdom 已知噪音，可忽略。
- 基线（2026-09-01）：13 文件 42 用例全绿；分布见 [TDD 用例分布表](./tdd.md)。

## 2. 为新页面写测试（标准流程）

1. **建文件**：`ui/src/pages/<page>/__tests__/X.test.tsx`（目录式 `__tests__` + `*.test.tsx`）。
2. **mock 依赖**（照抄 [Users.test.tsx](../../src/pages/users/__tests__/Users.test.tsx) 范式，[@FE-DT-S14](./bdd.md#fe-dt-s14)）：

```tsx
vi.mock('@/api/settings', () => ({
  getSettings: vi.fn().mockResolvedValue({ items: [{ key: 'oauth_auto_register', value: 'false' }] }),
  saveSetting: vi.fn().mockResolvedValue(undefined),
}))
```

页面 import 的**每个** `@/api/*` 都要 mock（漏一个即真实网络）；mock 返回值保持 Kiota 形状（`{ items, ... }`）。

3. **注入 store**（[@FE-DT-S15](./bdd.md#fe-dt-s15)）：`beforeEach(() => { vi.clearAllMocks(); useAppStore.setState({ userInfo: {...} }) })`；角色用例再改 `isAdmin`。
4. **渲染与断言**（[@FE-DT-S16](./bdd.md#fe-dt-s16)/[@FE-DT-S17](./bdd.md#fe-dt-s17)）：`render(<MemoryRouter><X /></MemoryRouter>)`；异步等 `waitFor`/`findByText` 不 sleep；重定向断言目标 DOM 不存在；交互用 user-event。
5. **回归**：`npm run test && npm run typecheck && npm run lint` 全绿，并在 [TDD 分布表](./tdd.md)补一行。

## 3. Dashboard / 样册页维护注意

- **统计是写死占位**（12/8/24/2048 + trend）：接真实 API 时 → `api/` 建封装 → Dashboard 数据驱动 + 加载态 → 顺手补 `__tests__`（mock 新 api）。
- 快捷入口 `/app` `/wiki` `/team` 与「插件」`/plugin` 均为**占位路由**，当前落 `*` 兜底回 `/dashboard`；新增真实页面在 `router/index.tsx` 受保护 children 注册并同步 `AppSider.pathToKey`。
- 欢迎横幅「开始使用」按钮**无 onClick**（as-built 现状），接入动作时补。
- `/design-system` 是**公开路由**：不得放真实业务数据/敏感信息；新增演示区块可后补 `ds.*` 词条。

## 4. 排障

| 现象 | 原因 | 处理 |
|---|---|---|
| 测试报 matchMedia 未定义 | 自定义入口绕过 setup | 确认 `test.setupFiles` 未改动；单测内局部再 mock |
| 测试真实发起网络请求 | 漏 mock 某个 `@/api/*` | 补 `vi.mock`（[@FE-DT-S14](./bdd.md#fe-dt-s14)） |
| 用例间数据串扰 | 未清 mock / store 残留 | beforeEach `vi.clearAllMocks()` + `setState` 重置 |
| tsc 报 vitest 类型缺失 | tsconfig types 未含 | `tsconfig.app.json` 应含 `vitest/globals`、`@testing-library/jest-dom` |
| 重定向断言不稳 | 依赖路由实例 | 改断言目标 DOM 不存在（[@FE-DT-S17](./bdd.md#fe-dt-s17)） |
| 测试整体偏慢（4s+） | jsdom + antd 体量 | 正常水位；避免无谓整页深渲染 |

## 5. 验收流程（发布前）

1. `cd ui && npm run lint && npm run test && npm run typecheck` 全绿，用例数不少于基线（当前 42）。
2. 走查 [@FE-DT-S1](./bdd.md#fe-dt-s1)~[@FE-DT-S5](./bdd.md#fe-dt-s5)（问候语带用户名、静态统计确认、快捷入口回概览、未登录被拦）与 [@FE-DT-S6](./bdd.md#fe-dt-s6)~[@FE-DT-S8](./bdd.md#fe-dt-s8)（样册免登录、13 区块齐全、主题开关生效）。
3. 新增页面必须附 `__tests__`（第 2 节流程）并在 [TDD](./tdd.md) 分布表补行。

## 6. 历史验收存档（L3 证据，保留原始记录）

- **2026-09-01 轮次 20 交付（as-built 回溯整理）**：`npm run test` 13 文件 42 用例全绿（Duration 4.21s，含 Users.test.tsx 3 用例 422ms）；`npm run typecheck` 无输出退出码 0；`npm run lint` 通过（`**/*.html` 与 client 生成码已 ignore）；Dashboard/样册手工走查通过，如实记录静态数据与占位路由边界。
- **2026-09-01 第二轮全系统深度测试**（存档于 [../../../docs/user-management/sop.md](../../../docs/user-management/sop.md)）：前端环境修复 `.env.development`(5000) 优先级高于 `.env.local`，新增 `ui/.env.development.local` 指向 5210；最终 Vitest 42/42。
- **2026-09-02 第三轮回归**：全页面浏览器回归通过；最终 68/68 + 36/36 + 42/42、构建 0 错。

## 7. 变更记录

| 日期 | 变更 |
|---|---|
| 2026-09-01 | 功能交付（轮次 20，as-built）；记录 Dashboard 静态数据与占位路由边界 |
| 2026-09-02 | 按 [DOC-STANDARD](../../../docs/DOC-STANDARD.md) 重构：场景编号化（FE-DT）、四件互链、职责瘦身 |
