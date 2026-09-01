# 前端 Dashboard 与测试基建（Dashboard & Testing）设计规格（SDD）

> 关联：[SDD](./sdd.md) ｜ [BDD](./bdd.md) ｜ [TDD](./tdd.md) ｜ [SOP](./sop.md) ｜ 上游：[../frontend-conventions.md](../frontend-conventions.md)、[../design-system/](../design-system/)（组件与 token 来源） ｜ 证据：`cd ui && npm run test`（13 文件 42 用例）
> 规范：[../../../docs/DOC-STANDARD.md](../../../docs/DOC-STANDARD.md)。行为场景见 BDD（@FE-DT-Sxx），本文不重复。

## 目标

两块互相支撑的内容：**Dashboard**（`/dashboard`，登录后首页）与**测试基建**（Vitest + Testing Library 的配置、setup、mock 约定）；设计系统活体样册 `DesignSystemPreview`（`/design-system`）一并归档。

## Dashboard（`ui/src/pages/Dashboard.tsx`）

结构自上而下：`Page` 头部（title「概览」+ subtitle 插值 `nickName ?? userName ?? app.name` + extra「新建应用」按钮）→ 渐变欢迎横幅（硬编码蓝渐变 + 白字 +「开始使用」按钮**未绑定点击事件**）→ 四张 StatCard → 快捷入口卡（三行：新建应用/上传文档/邀请成员，`navigate` 跳 `/app` `/wiki` `/team`）→ 最近动态卡（`Empty` 空态 +「查看全部」link）。

数据边界（as-built）：挂载时 `refreshUserProfile().catch(() => undefined)` 刷新档案一次；**四个统计值与趋势（12/8/24/2048 与 8/12/4/22）全部为组件内写死的静态数字，未接任何 API**（[@FE-DT-S3](./bdd.md#fe-dt-s3)）；快捷入口指向的 `/app` `/wiki` `/team` 无路由，落 `*` 兜底回 `/dashboard`（占位导航，[@FE-DT-S4](./bdd.md#fe-dt-s4)）。

## DesignSystemPreview（`/design-system`）

- **公开路由**（与 /login、/register 平级，不经 RequireAuth），便于设计走查；不承载任何业务数据。
- 单文件 13 个 Section：色彩令牌、间距/字号、按钮、表单控件、Tag/Badge、StatCard、主题切换（读写 store 的 themeKey/toggleTheme）、QueryBar、FormPage、DataTable、Feedback、Chat、五个页面模板。
- 定位是开发工具页：演示文案大多为硬编码中文（`ds.*` i18n 仅 8 键）。

## 路由上下文

```
/login /register /oauth_login /design-system  → 公开（无 RequireAuth）
/（RequireAuth + AppLayout）                   → 受保护
  ├ index → Navigate /dashboard
  ├ dashboard | account | users | settings | oauthconnect
  └ * → Navigate /dashboard                    ← /app /wiki /team /plugin 落点
```

## 测试基建设计

| 分类 | 选型（package.json 真实取值） |
|---|---|
| 运行器 | vitest ^4.1.11（scripts：`test` = `vitest run`、`test:watch` = `vitest`） |
| DOM 环境 | jsdom ^29.1.1 |
| 组件测试 | @testing-library/react ^16.3.3 + user-event ^14.6.6 |
| 断言扩展 | @testing-library/jest-dom ^7.0.1 |
| 类型检查 | `typecheck` = `tsc -b --noEmit` |

- **无独立 vitest.config**：测试段内联在 `vite.config.ts` 的 `test`（jsdom / globals / `setupFiles: ['./src/test/setup.ts']` / css:false），复用 `@` 别名与 react 插件；`tsconfig.app.json` `types: ["vitest/globals", "@testing-library/jest-dom"]`。
- **setup.ts 全局两件事**：注册 jest-dom 匹配器；mock `window.matchMedia`（matches 恒 false + 全量方法），满足 antd 响应式组件与 store 主题初始化（[@FE-DT-S12](./bdd.md#fe-dt-s12)/[@FE-DT-S13](./bdd.md#fe-dt-s13)）。

### Mock 约定（事实标准，提炼自 Users.test.tsx）

1. API 模块**整模块 mock**：`vi.mock('@/api/*', ...)`，返回值保持 Kiota 响应形状（`{ items, totalCount }`）；页面依赖的每个 api 都要 mock（[@FE-DT-S14](./bdd.md#fe-dt-s14)）。
2. store 直写：`useAppStore.setState({ userInfo })` 注入登录态/角色，`beforeEach` 里 `vi.clearAllMocks()` 后重设（[@FE-DT-S15](./bdd.md#fe-dt-s15)）。
3. 路由组件包 `MemoryRouter`；重定向断言用副作用（目标 DOM 不存在），不断言路由实例（[@FE-DT-S17](./bdd.md#fe-dt-s17)）。
4. 异步数据用 `waitFor`/`findByText`，不 sleep（[@FE-DT-S16](./bdd.md#fe-dt-s16)）。

## 已知问题

- Dashboard 统计为静态假数据；欢迎横幅「开始使用」按钮无 onClick。
- `/design-system` 无鉴权且含大量硬编码演示文案，不得作为业务页范本、不得放真实数据。
- **业务页测试覆盖缺口**：仅 Users 有测试（design-system 11 文件 + theme 2 文件 + users 1 文件 = 13 文件 42 用例，分布见 [TDD](./tdd.md)）；Dashboard/AccountSettings/Settings/OauthConnect/Login/Register 均无，补测试流程见 [SOP 第 2 节](./sop.md)。
- 测试 setup 常驻 3.5s+（jsdom + antd 体量）属正常水位；jsdom 会打印 `getComputedStyle ... pseudo-elements` 未实现噪音，不影响断言。
