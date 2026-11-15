# 前端认证流（auth-flow）验证映射（TDD）

> 关联：[SDD](./sdd.md) ｜ [BDD](./bdd.md)（场景定义） ｜ [TDD](./tdd.md) ｜ [SOP](./sop.md)
> 本文只做「场景 → 验证物 → 结果」映射；场景语义见 BDD 编号，设计依据见 SDD。

## 场景映射表

| 场景 | 验证物 | 结果（日期） |
|---|---|---|
| @FE-AUTH-S20 | [local-dev/user-management-e2e.mjs](../../../../local-dev/user-management-e2e.mjs)（脚本以与前端一致的 JSEncrypt PKCS1 加密走登录/注册/重置链路，真实 HTTP） | PASS 34/34（2026-09-02） |
| @FE-AUTH-S6（后端裁决段） | 同上脚本注册弱密码用例（400） | PASS（2026-09-02，随 34/34） |
| @FE-AUTH-S8 ~ @FE-AUTH-S11 | `ui/src/auth/RequireAuth.tsx` + `ui/src/utils/jwt.ts` + `ui/src/api/auth.ts` 代码走查（提前 5 分钟续期、checkToken 四分支、60s 周期） | PASS（2026-11-15，代码级） |
| @FE-AUTH-S10、@FE-AUTH-S24 ~ @FE-AUTH-S26 | `ui/src/utils/__tests__/jwt.test.ts`（5 例：默认过期判断/提前量/坏 token）+ `ui/src/api/__tests__/authToken.test.ts`（7 例：有效期充足不请求、提前续期并保留资料、并发单飞、续期异常沿用会话、异常且已过期判失败、refresh 过期不发请求、续期响应缺 accessToken 判失败） | PASS（2026-11-15，vitest 12/12） |
| @FE-AUTH-S27 | `ui/src/api/__tests__/sessionRetry.test.ts`（真实 Kiota 客户端 + 全局 fetch 桩：业务请求首次 401 → 触发 `/auth/refresh_token` → 用新 token 重试并返回 200，断言重试请求 Authorization 已是新 token、store 已更新） | PASS（2026-11-15） |
| @FE-AUTH-S22 ~ @FE-AUTH-S23 | 浏览器走查（Playwright/Edge）：admin 登录后 store 即含 isAdmin/isRoot 且管理菜单出现；篡改 localStorage 快照（isAdmin=false/nickName=stale-name）后刷新 → 服务端资料校正回来；typecheck/lint 全绿 | PASS（2026-09-29，走查） |
| @FE-AUTH-S12 ~ @FE-AUTH-S19 | `ui/src/pages/auth/OAuthLogin.tsx` + `ui/src/utils/oauth.ts` 走查（bindMode/未绑定/缺参/StrictMode ref） | PASS（2026-09-01，代码级） |
| 公开路由可达性（支撑 S1/S5/S12） | `ui/src/router/index.tsx` 走查：/login、/register、/oauth_login 顶层公开；/ 由 RequireAuth 包裹 | PASS（2026-09-01，走查） |
| @FE-AUTH-S1 ~ S5、S7、S21 | @manual 浏览器走查（[SOP 第 4 节](./sop.md)） | PASS（2026-09-01） |

## 回归命令

```bash
cd ui && npm run typecheck && npm run lint && npm run test   # 三件套（2026-09-29 实测 446/446）
node local-dev/user-management-e2e.mjs                       # 需后端运行于 :5210（2026-09-02 实测 34/34）
```

## 覆盖率说明（缺口如实记录）

- 登录/注册/OAuth 回调/RequireAuth 均无组件级 Vitest（现有 42 用例集中在设计系统组件与 Users 页），页面行为依赖走查。
- `utils/jwt.ts`（过期判断/提前量）与 `api/auth.ts` 的续期分支已有单测（`utils/__tests__/jwt.test.ts`、`api/__tests__/authToken.test.ts`）；`utils/oauth.ts`（state 协议）仍以代码走查替代。
- RequireAuth 组件的休眠恢复补检（@FE-AUTH-S25）依赖浏览器事件，暂无组件级用例，以代码走查 + 浏览器走查（SOP 第 4 节）验证。
