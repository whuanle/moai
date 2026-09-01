# 前端布局导航与路由（layout-routing）设计规格（SDD）

> 关联：[SDD](./sdd.md) ｜ [BDD](./bdd.md) ｜ [TDD](./tdd.md) ｜ [SOP](./sop.md) ｜ 上游：[../../docs/auth-flow/sdd.md](../../docs/auth-flow/sdd.md)（认证守卫与 checkToken）、[../../docs/store-i18n/sdd.md](../../docs/store-i18n/sdd.md)（用户态与偏好持久化） ｜ 下游：[../../docs/components-base/sdd.md](../../docs/components-base/sdd.md)、[../../docs/components-form/sdd.md](../../docs/components-form/sdd.md)（页面构成） ｜ 证据：[路由 curl 实测](./tdd.md)、[消费方单测](../../src/pages/users/__tests__/Users.test.tsx)
> 规范：[DOC-STANDARD](../../../docs/DOC-STANDARD.md)。行为场景见 BDD（@FE-LR-Sxx），本文不重复。

## 目标

应用骨架：路由表（公开页 + 受保护布局）、RequireAuth 登录守卫、AppLayout/AppSider 导航壳、`/design-system` 展示台。挂载链：`main.tsx`（StrictMode → AppProviders）→ `App.tsx`（RouterProvider）→ 路由表。

## 路由表（`ui/src/router/index.tsx`，react-router v7 createBrowserRouter）

| 路径 | 组件 | 守卫 | 说明 |
|---|---|---|---|
| /login、/register、/oauth_login、/design-system | 对应页 | 无 | 公开页；design-system 为设计系统活文档 |
| / | RequireAuth→AppLayout | 登录态 + 60s 周期 checkToken | 子路由经 Outlet 渲染 |
| ├ index、* | Navigate /dashboard | | 兜底重定向（replace） |
| ├ /dashboard、/account | Dashboard、AccountSettings | | 全员 |
| ├ /users、/settings、/oauthconnect | 对应页 | 页面内再判 isAdmin（非管理员 Navigate 回 /dashboard） | 管理员 |

约定（router 注释）：新业务页追加在 children；页面级权限在页面内 `isAdmin/isRoot` 判断 + Navigate 兜底，**接口层才是最终防线**。

## AppLayout / AppSider

- 双栏：外层 Layout(minHeight:100vh) → AppSider + 内层 Content(padding:24, Outlet)。
- Sider 五区（上→下）：品牌区（logo 28×28 + 「MoAI」）；用户卡（Dropdown click 触发：Avatar 34（avatar 缺省首字母/U 兜底）+ 主行 `nickName ?? userName` + 副行 `email ?? userName`；菜单「设置」→/account、divider、「退出登录」→clearUserInfo + /login）；主导航 Menu（inline，flex:1 滚动；mainNav = dashboard/app/wiki/team）；管理导航（**仅 `userInfo?.isAdmin === true`** 渲染 divider + adminNav = plugin/users/oauthconnect/settings，普通用户完全不可见）；底部双 Select（主题 light/dark 与语言 zh-CN/en-US，行为归属 [../theme/sdd.md](../theme/sdd.md) 与 [../../docs/store-i18n/sdd.md](../../docs/store-i18n/sdd.md)）。
- Sider 本体：width 232、theme 跟随明暗、sticky top:0 height:100vh、右边框按明暗切 `rgba(255,255,255,.08)` / `rgba(16,24,40,.08)`。
- 选中态：`pathToKey` 精确映射（8 键齐全，含未实现项），未命中回 dashboard 键；点击回调在 `[...mainNav, ...adminNav]` 查 key 后 navigate。
- 菜单文案走 `nav.*` i18n 键（overview 概览 / app 应用 / wiki 知识库 / team 团队 / plugin 插件 / users 用户 / oauthconnect 第三方登录 / settings 设置）。

## 守卫链

`RequireAuth.tsx`（`TOKEN_CHECK_INTERVAL = 60_000`，认证语义见上游 [../../docs/auth-flow/sdd.md](../../docs/auth-flow/sdd.md)）：

1. 无 accessToken（store）→ 同步 `<Navigate to="/login" replace/>`（不发请求）；
2. 挂载执行一次 checkToken（异常按失败计）：失败 → clearUserInfo + replace 跳 /login（active 标志防卸载后 setState）；成功 → 放行 children；
3. 每 60 秒 setInterval 重复 checkToken；
4. 检查期间渲染全屏 Spin(large)；cleanup 清 interval 并置 active=false。

请求侧拦截在 `api/kiota.ts` 中间件：401 且非 login 接口 → 清态跳 /login（不发 toast）。

## /design-system 展示台

公开路由（不在 RequireAuth 内），设计系统可视化活文档：顶部 Alert（控件高度 36/圆角 8/内边距 16）+ Section 演示——色彩令牌、间距/字号、按钮、表单控件、Tag/Badge、StatCard、主题切换、QueryBar、FormPage（含校验）、DataTable（mock + toolbar + onRefresh + 分页）、Feedback（message/notification/modal + Popconfirm）、Chat、五种页面模板。

## 已知问题（as-built，未改代码）

1. **规划中的 /app /wiki /team /plugin 无路由实现**：菜单点击后 pathname 落到 `*` 兜底回 /dashboard（[@FE-LR-S11](./bdd.md#fe-lr-s11)），体验上「点了没反应」；建议未实现项禁用或标 coming soon。pathToKey 本身含这四个键，缺的是路由表项。
2. 页面级权限靠页面自判（Navigate），菜单仅按 isAdmin 折叠——root 专属菜单项（settings 对 root）无区分。
3. [frontend-conventions.md](../frontend-conventions.md) 与现状不符：目录结构写有 `layouts/components/AppHeader.tsx` 但实际不存在；「主题切换入口统一放 AppHeader（Switch）」与现状不符（现为 Sider 底部 Select，用户菜单/语言切换也在 Sider）。行为以代码为准。
4. `/design-system` 为**无鉴权公开路由**，演示页含 mock 数据与大量硬编码中文演示文案（仅 Alert/部分提示走 t()）；不希望对外暴露需在网关或路由层处理。
5. i18n 中 `nav.chat`（全量 AI）/`nav.model`（模型）/`nav.admin`（管理）键存在但当前菜单未使用。
