# 前端认证流（auth-flow）操作手册（SOP）

> 关联：[SDD](./sdd.md) ｜ [BDD](./bdd.md)（验收场景编号） ｜ [TDD](./tdd.md)（验证映射与回归命令） ｜ [SOP](./sop.md)

## 1. 排障

| 现象 | 定位 | 处理 | 场景 |
|---|---|---|---|
| 登录报"密码解密失败" | 持久化的 rsaPublic 过期（后端换密钥） | 清 localStorage `moai-web-store` 重新登录；根治需加时效 | [@FE-AUTH-S4](./bdd.md#fe-auth-s4) |
| 登录成功又立刻被踢回 /login | access+refresh 同时过期，或某接口 401 | 看 Network 哪个接口 401；后端重启后 token 失效属预期 | [@FE-AUTH-S11](./bdd.md#fe-auth-s11) |
| 第三方回调停在 loading | code 被消费两次或 state 解析失败 | 确认 StrictMode ref 守卫生效；检查回调 URL 的 state | [@FE-AUTH-S15](./bdd.md#fe-auth-s15) |
| 绑定弹窗无反应 | opener 跨域 → 走了登录分支 | 保证账号设置页与弹窗同 origin | [@FE-AUTH-S19](./bdd.md#fe-auth-s19) |
| 页面频繁闪跳 /login | checkToken 周期失败 | 检查 refresh token 是否被后端拒（用户被禁用/删除） | [@FE-AUTH-S10](./bdd.md#fe-auth-s10) |

## 2. 联调注意

- 后端 DEBUG 编译下 access token 7 天有效——联调"续期"需 Release 或临时改 TokenProvider。
- 本地后端 5210：`ui/.env.local` 设 `VITE_ServerUrl=http://127.0.0.1:5210`（`.env.development` 遗留 5000，`.env.local` 优先级更高；删除该变量则回退同源模式）。
- 清登录态：DevTools → Application → Local Storage → 删 `moai-web-store`、`moai-web-theme`、`moai-web-locale`。

## 3. 新增页面接入认证

1. 页面 `ui/src/pages/<domain>/Xxx.tsx`，按 [../frontend-conventions.md](../frontend-conventions.md) 页面规范。
2. API 封装 `ui/src/api/<domain>.ts`（鉴权/匿名工厂见 [../api-layer/sop.md](../api-layer/sop.md)）。
3. 注册路由（关键）：受保护页加到 `/` 的 children（RequireAuth+AppLayout 内）；公开页加到顶层与 /login 同级。
4. i18n：`ui/src/i18n/locales/{zh-CN,en-US}/common.json`；侧边栏入口在 AppSider 的 mainNav/adminNav（admin 仅 `userInfo.isAdmin` 可见）。
5. 自检：`cd ui && npm run typecheck && npm run lint && npm run test`。

## 4. 验收流程（认证行为改动后）

1. 未登录访问：清 localStorage 后打开受路由 URL → 重定向 /login（[@FE-AUTH-S8](./bdd.md#fe-auth-s8)）。
2. 登录/注册走查：正确凭据落 /dashboard；错误密码停留本页仅提示（[@FE-AUTH-S1](./bdd.md#fe-auth-s1)/[@FE-AUTH-S2](./bdd.md#fe-auth-s2)）；注册两次不一致被拦（[@FE-AUTH-S5](./bdd.md#fe-auth-s5)）。
3. 续期无感：人工把 store 里 accessToken 置为过期 JWT 后等下一周期 → 出现 refresh 请求且页面不跳转（[@FE-AUTH-S10](./bdd.md#fe-auth-s10)）。
4. 登出/失效：退出清态回 /login；改坏 refreshToken 刷新 → 清态回 /login（[@FE-AUTH-S11](./bdd.md#fe-auth-s11)）。
5. 401 拦截：篡改 accessToken 后触发业务请求 → 整页跳 /login（[@FE-AUTH-S21](./bdd.md#fe-auth-s21)）。
6. 回归命令见 [TDD](./tdd.md)（typecheck/lint/test + E2E）。

## 5. 历史验收存档（L3 证据，保留原始记录）

- **2026-09-01（轮 11，as-built）**：`npm run typecheck`（tsc -b --noEmit）退出码 0；`npm run lint` 0 告警；`npm run test` 13 文件 42 用例全过（含 Users 页 3 例）；jwt.ts 走查确认宽限 60s 与 checkToken 三分支（api/auth.ts:141-148）；OAuthLogin.tsx:31-91 走查确认 bindMode/parseOAuthState 兼容无冒号 state/useRef 防重；router 走查确认公开路由与 RequireAuth 包裹。RSA 兼容链路证据：轮 1/轮 2 E2E 历史记录 34/34（当时脚本未入仓）。
- **2026-09-02（E2E 脚本就位后）**：[local-dev/user-management-e2e.mjs](../../../../local-dev/user-management-e2e.mjs) 以与前端一致的 RSA 加密路径全绿 34/34，[@FE-AUTH-S20](./bdd.md#fe-auth-s20) 由历史记录转为可复现证据。

## 6. 变更记录

| 日期 | 变更 |
|---|---|
| 2026-09-01 | 初版（轮 11，as-built）；同日补强：新增页面接入认证、验收流程；修正 rsaEncrypt 归一化描述与 oauthBindAccount 现状 |
| 2026-09-02 | 按 [DOC-STANDARD](../../../docs/DOC-STANDARD.md) 重构：场景编号化（@FE-AUTH-S1~S21）、四件互链、职责瘦身；E2E 证据落位 |
