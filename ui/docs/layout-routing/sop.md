# 前端布局导航与路由（layout-routing）操作手册（SOP）

> 关联：[SDD](./sdd.md) ｜ [BDD](./bdd.md)（验收场景编号） ｜ [TDD](./tdd.md)（自动化映射） ｜ [SOP](./sop.md)
> 页面内部套路见 [../../docs/components-base/sop.md](../../docs/components-base/sop.md) 与 [../../docs/components-form/sop.md](../../docs/components-form/sop.md)。

## 1. 新增业务页面（标准动作）

1. `pages/<domain>/Xxx.tsx` 写页面（用 design-system 组件）。
2. `src/router/index.tsx` children 追加 `{ path: 'xxx', element: <Xxx /> }`。
3. 需要菜单入口：`AppSider.tsx` 的 mainNav/adminNav 加 NavItem + i18n `nav.xxx` 两个语言包 + `pathToKey` 映射。NavItem 四要素：key / icon / labelKey（`nav.*`）/ path。**归属**：全员入口进 mainNav；管理入口进 adminNav（自动获得 isAdmin 可见性 + 分隔线）。
4. 受限页面：页面内 `isAdmin/isRoot` 判断 + `<Navigate to="/dashboard" replace/>`（样板 `Users.tsx`：`if (!isAdmin) return <Navigate .../>`，且 `useEffect([isAdmin])` 里仅 isAdmin 为真才拉数据——管理员身份变更后刷新即收敛；`Settings.tsx`/`OauthConnect.tsx` 同款）。菜单折叠只是体验，**接口层才是最终防线**（[@FE-LR-S18](./bdd.md#fe-lr-s18)）。

参照实例：`ui/src/pages/users/Users.tsx`、`ui/src/pages/oauthconnect/OauthConnect.tsx`。

## 2. 排障

| 现象 | 原因 | 处理 | 场景 |
|---|---|---|---|
| 菜单点击后回概览页 | 目标路由未注册（走 `*` 兜底）——检查 router 与 pathToKey；app/wiki/team/plugin 属已知未实现项 | 补路由或暂缓 | [@FE-LR-S11](./bdd.md#fe-lr-s11) |
| 管理菜单不出现 | store userInfo.isAdmin 非 true | 重新登录/刷新用户态 | [@FE-LR-S9](./bdd.md#fe-lr-s9) |
| 退出后仍能回退访问 | 前端 Navigate 已挡；直接改 URL 会被守卫拦（无 token） | 无需处理 | [@FE-LR-S1](./bdd.md#fe-lr-s1)/[@FE-LR-S14](./bdd.md#fe-lr-s14) |
| 页面空白全屏加载不消失 | checkToken 未返回 | 查 refresh_token 是否有效、后端刷新接口可达 | [@FE-LR-S2](./bdd.md#fe-lr-s2) |
| 60 秒被踢出 | 周期 checkToken 失败（刷新失败） | 看 Network 面板 refresh_token 响应 | [@FE-LR-S3](./bdd.md#fe-lr-s3) |
| 主题/语言选择不记忆 | localStorage 键 `moai-web-theme` / `moai-web-locale` | 清缓存或查 store 动作 | [@FE-LR-S15](./bdd.md#fe-lr-s15)/[@FE-LR-S16](./bdd.md#fe-lr-s16) |

## 3. 验收流程（改路由/布局后）

1. 自动化：跑 [TDD 回归命令](./tdd.md)（消费方 3 用例 + 全量 42 + lint/typecheck + 10 路由 curl）。
2. 手工走查（`npm run dev`）：
   - 未登录直接开 `/users` → 跳 `/login`；登录普通用户开 `/users` → 跳 `/dashboard`（[@FE-LR-S1](./bdd.md#fe-lr-s1)/[@FE-LR-S18](./bdd.md#fe-lr-s18)）；
   - 普通用户侧栏无管理区；admin/root 出现管理区（[@FE-LR-S8](./bdd.md#fe-lr-s8)/[@FE-LR-S9](./bdd.md#fe-lr-s9)）；
   - 用户卡：设置 → `/account`；退出 → `/login`（[@FE-LR-S13](./bdd.md#fe-lr-s13)/[@FE-LR-S14](./bdd.md#fe-lr-s14)）；
   - 底部切 暗色/English → 全站即时生效，刷新保持（[@FE-LR-S15](./bdd.md#fe-lr-s15)/[@FE-LR-S16](./bdd.md#fe-lr-s16)）；
   - `/design-system` 匿名可访问且各演示段正常（[@FE-LR-S17](./bdd.md#fe-lr-s17)）。

## 4. 历史验收存档（L3 证据，保留原始记录）

- **2026-09-01 交付验收（轮 16，as-built）**：路由 9 项 curl 全 200（Vite dev 实测）；消费方 `npx vitest run src/pages/users` 1 file / 3 tests；全量 13 files / 42 tests；lint 0 error / 0 warning。pathToKey/路由表对照复核，**更正既有 T5 的「pathToKey 缺键」误记**：pathToKey 实际包含 /app /wiki /team /plugin 四键（AppSider.tsx:44-53），缺的是路由表项——点击后 pathname 落 `*` 兜底回 /dashboard，高亮回「概览」。发现并记录：frontend-conventions 的 AppHeader 描述与现状不符、/design-system 为公开路由（见 [SDD 已知问题](./sdd.md)）。
- **2026-09-01 文档标准重构回归**：10 路由 curl 复测全 200（含 /app）；消费方 3/3 复测通过。

## 5. 变更记录

| 日期 | 变更 |
|---|---|
| 2026-09-01 | 初版（轮 16，as-built）；同日按 [DOC-STANDARD](../../../docs/DOC-STANDARD.md) 重构：场景编号化（FE-LR-S1~S18）、四件互链、职责瘦身 |
