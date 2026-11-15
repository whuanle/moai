# 前端认证流（auth-flow）设计规格（SDD）

> 关联：[SDD](./sdd.md) ｜ [BDD](./bdd.md) ｜ [TDD](./tdd.md) ｜ [SOP](./sop.md) ｜ 上游：[../../../docs/auth/sdd.md](../../../docs/auth/sdd.md)、[../../../docs/account/sdd.md](../../../docs/account/sdd.md) ｜ 证据：[local-dev/user-management-e2e.mjs](../../../../local-dev/user-management-e2e.mjs)
> 规范：[../../../docs/DOC-STANDARD.md](../../../docs/DOC-STANDARD.md)、[../frontend-conventions.md](../frontend-conventions.md)（仅引用）。行为场景见 BDD（@FE-AUTH-Sxx），本文不重复。

## 目标

登录/注册/第三方登录/绑定/续期的前端闭环。代码：`ui/src/api/auth.ts`、`ui/src/pages/auth/{Login,Register,OAuthLogin}.tsx`、`ui/src/auth/RequireAuth.tsx`、`ui/src/utils/{rsa,jwt,oauth}.ts`、`ui/src/store/app.ts`（UserInfo）。路由接线（`ui/src/router/index.tsx`）：公开 `/login`、`/register`、`/oauth_login`；`/` 由 `<RequireAuth><AppLayout/></RequireAuth>` 包裹，子路由 dashboard/account/users/settings/oauthconnect。

## 流程总览

```
密码登录:  Login → login() → getServerInfo(取 rsaPublic,持久缓存) → rsaEncrypt → POST /auth/login → setUserInfo(仅 token) → /apps
注册:      Register → register()(RSA 密码) → POST /auth/register → 跳 /login
第三方登录: Login 图标 → 跳后端拼好的 authorizeUrl(state={OAuthId})
           → 回调 /oauth_login?code&state → oauthLogin()
             ├ isBindUser=true → applyLoginResponse(仅 token) → /apps
             └ false → 「一键注册」卡 → oauthRegister(tempOAuthBindId)
绑定弹窗:  AccountSettings window.open(toBindAuthorizeUrl(url))  # state 附加 :bind
           → 回调页识别 opener+bind → oauthBindByCode(code) → postMessage 通知 + window.close
续期:      RequireAuth 每 60s checkToken() → 剩余不足 5 分钟（或已过期）→ refresh_token 静默换新 → 失败清态跳 /login
资料重取:  RequireAuth 挂载首检通过后 refreshUserProfile()（GET /account/userinfo 合并进登录态）→ 失败沿用本地快照
```

## 关键实现点（as-built）

| 点 | 行为 |
|---|---|
| RSA 加密 | `utils/rsa.ts` JSEncrypt PKCS1，密文 Base64；末尾 `btoa(atob(x))` 为 Base64 归一化（恒等变换）；失败抛 `Encryption failed`（[@FE-AUTH-S20](./bdd.md#fe-auth-s20)） |
| token 过期判断 | `utils/jwt.ts isTokenExpired(token, skewSeconds=0)`：`exp - now <= skewSeconds` 即视为需要续期；`checkToken` 传 300 秒（提前 5 分钟续期），刷新令牌判断传 0（只判真实过期）；解码失败视为过期 |
| checkToken | 无 accessToken→false；access 剩余有效期 > 5 分钟→true（不发请求）；refresh 缺失/已过期→false；否则静默续期（单飞：并发复检只发一次 refresh 请求）→ 成功合并登录态；续期请求抛错（网络/5xx/反代 502）时只要 access token 尚未真正过期就返回 true（下轮再试），避免一次抖动被登出 |
| expiresIn 语义 | 后端返回过期时刻 Unix 毫秒时间戳；前端原样保存但不参与过期判断，一律解码 JWT 的 exp（见 [../../../docs/auth/sdd.md](../../../docs/auth/sdd.md)） |
| 401 全局拦截 | api/kiota.ts FilterRequestHandler：鉴权接口 401 先调注册的静默续期（`registerSessionRefreshHandler`，实现见 `api/auth.ts`）并**用新 token 重试一次**，重试后仍 401 才清态 + 整页跳 `/login`；`/auth/login`、`/auth/oauth_login`、`/auth/refresh_token` 的 401 不参与续期重试（只提示/按续期失败处理），避免递归（[@FE-AUTH-S21](./bdd.md#fe-auth-s21)/[@FE-AUTH-S27](./bdd.md#fe-auth-s27)） |
| 续期落态 | 续期响应只含 token 与基本标识：`refreshAccessToken` 按 `{...当前登录态, ...新响应}` 合并，保留 isAdmin/isRoot/头像昵称，避免静默续期后管理菜单消失 |
| 提前续期 | 线上 Release 构建 access token 仅 30 分钟（DEBUG 7 天）：剩余不足 300 秒即换新，不在过期后裸奔，避免任意业务请求 401 触发强制登出（[@FE-AUTH-S24](./bdd.md#fe-auth-s24)） |
| UserInfo 持久化 | zustand persist 仅存 serverInfo+userInfo（localStorage `moai-web-store`）；theme/locale 另存 |
| 资料重取 | 登录/续期响应只含 token 与基本标识，不含 isAdmin/isRoot/头像昵称；RequireAuth 挂载首检通过后调 `refreshUserProfile()`（GET /account/userinfo）合并进登录态，失败静默沿用本地快照（[@FE-AUTH-S22](./bdd.md#fe-auth-s22)/[@FE-AUTH-S23](./bdd.md#fe-auth-s23)）；60s 周期复检不重取资料 |
| 权限消费 | 页面权限（AppSider adminNav、/users 操作）读 `userInfo?.isAdmin/isRoot`，由 RequireAuth 挂载时 `refreshUserProfile()` 重取维护；前端不解 JWT claims |
| OAuth code 一次性 | OAuthLogin 用 `useRef(started)` 防 StrictMode 双执行重复消费 code（[@FE-AUTH-S15](./bdd.md#fe-auth-s15)） |
| state 协议 | `state={OAuthId}` 登录 / `{OAuthId}:bind` 绑定弹窗（parseOAuthState 解析；toBindAuthorizeUrl 重写） |
| 弹窗通信 | postMessage `oauth_bind_{success|error|cancel}`（仅同源 opener），主窗口 AccountSettings 监听后刷新绑定列表 |

## 页面行为（as-built 摘要）

- **Login.tsx**：400 宽 Card（品牌区：logo 48px + 标题 + 副标题；背景 `linear-gradient(colorBgLayout→colorPrimaryBg)` 跟随明暗主题，卡片 radius.lg+主题阴影），username/password 必填；成功 feedback.success 后 `navigate('/apps',{replace:true})`，失败依赖全局中间件提示。挂载即拉 `getOAuthProviders()`（后端路由拼写即 `oauth_prividers`）：有渠道渲染圆形图标按钮（iconUrl 经 resolveStorageUrl 解析，无图标显示名称文本），点击整页跳授权；分隔线用 `colorBorderSecondary` token。
- **Register.tsx**：420 宽 Card，与 Login 同款品牌区/主题自适应渐变背景（2026-09-02 统一）；userName 必填、nickName/phone 选填、email 必填（antd type:'email'）、password 仅 `min:6`、confirmPassword 校验两次一致（不一致报 `auth.passwordMismatch`）。强度（8-20 位含字母+数字）由后端解密后裁决。
- **OAuthLogin.tsx**：`isPopup = opener 存在且同源`、`bindMode = isPopup && state 带 bind`。bindMode：缺参通知 cancel 关窗；否则 oauthBindByCode（鉴权客户端，绑定当前登录账号）。顶层：缺参/异常/无 tempOAuthBindId → 跳 /login；isBindUser → 落态进 /apps；有 tempOAuthBindId → 「一键注册」确认卡。
- **RequireAuth.tsx**：无 token 渲染期同步 `<Navigate to="/login" replace/>`；挂载执行一次 checkToken（异常按失败），通过后调 `refreshUserProfile()` 重取用户资料（失败静默沿用快照）；每 60 秒（TOKEN_CHECK_INTERVAL=60_000）复检仅做 token 检查；`visibilitychange`（回到可见）/`focus`/`online`/`pageshow` 触发补检（5 秒内不重复），覆盖休眠与后台挂起后定时器停滞、恢复首个业务请求带过期 token 的场景（[@FE-AUTH-S25](./bdd.md#fe-auth-s25)）；检查期间整屏 Spin；卸载清定时器与监听。

## 已知问题

1. `getServerInfo` 持久缓存 rsaPublic：后端轮换密钥后旧缓存导致"密码解密失败"（[@FE-AUTH-S4](./bdd.md#fe-auth-s4)），需清 localStorage 或加时效。
2. 401 跳转是 `window.location.href` 整页刷新（丢 SPA 状态）且不携带回跳参数，登录后固定进 /dashboard（[@FE-AUTH-S21](./bdd.md#fe-auth-s21)）。
3. `oauthBindAccount()` 封装（POST /api/account/oauth_bind_account）当前无页面调用——实际绑定走 `oauthBindByCode`（POST /api/account/oauth_bind），as-built 记录为遗留封装。
4. 登录/注册/OAuth 回调/RequireAuth 无组件级 Vitest（现有用例集中在设计系统组件与 Users 等页面），行为验证依赖 typecheck/lint + 手工/浏览器走查（见 [TDD](./tdd.md) 缺口说明）。
5. 401 豁免条件为 URL 字符串包含 `login`：`/auth/refresh_token` 若返回 401 会触发清态跳转（续期端点自身失败即会话失效，属预期；401 续期重试对 `login`/`refresh_token` 端点不再递归）；更细的路径匹配仍待做（详见 [../api-layer/sdd.md](../api-layer/sdd.md)）。
