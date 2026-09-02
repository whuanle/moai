# 前端认证流（auth-flow）验证映射（TDD）

> 关联：[SDD](./sdd.md) ｜ [BDD](./bdd.md)（场景定义） ｜ [TDD](./tdd.md) ｜ [SOP](./sop.md)
> 本文只做「场景 → 验证物 → 结果」映射；场景语义见 BDD 编号，设计依据见 SDD。

## 场景映射表

| 场景 | 验证物 | 结果（日期） |
|---|---|---|
| @FE-AUTH-S20 | [local-dev/user-management-e2e.mjs](../../../../local-dev/user-management-e2e.mjs)（脚本以与前端一致的 JSEncrypt PKCS1 加密走登录/注册/重置链路，真实 HTTP） | PASS 34/34（2026-09-02） |
| @FE-AUTH-S6（后端裁决段） | 同上脚本注册弱密码用例（400） | PASS（2026-09-02，随 34/34） |
| @FE-AUTH-S8 ~ @FE-AUTH-S11 | `ui/src/auth/RequireAuth.tsx` + `ui/src/utils/jwt.ts` 代码走查（宽限 60s、checkToken 三分支、60s 周期） | PASS（2026-09-01，代码级） |
| @FE-AUTH-S12 ~ @FE-AUTH-S19 | `ui/src/pages/auth/OAuthLogin.tsx` + `ui/src/utils/oauth.ts` 走查（bindMode/未绑定/缺参/StrictMode ref） | PASS（2026-09-01，代码级） |
| 公开路由可达性（支撑 S1/S5/S12） | `ui/src/router/index.tsx` 走查：/login、/register、/oauth_login 顶层公开；/ 由 RequireAuth 包裹 | PASS（2026-09-01，走查） |
| @FE-AUTH-S1 ~ S5、S7、S21 | @manual 浏览器走查（[SOP 第 4 节](./sop.md)） | PASS（2026-09-01） |

## 回归命令

```bash
cd ui && npm run typecheck && npm run lint && npm run test   # 三件套（2026-09-01 实测 42/42）
node local-dev/user-management-e2e.mjs                       # 需后端运行于 :5210（2026-09-02 实测 34/34）
```

## 覆盖率说明（缺口如实记录）

- 登录/注册/OAuth 回调/RequireAuth 均无组件级 Vitest（现有 42 用例集中在设计系统组件与 Users 页），页面行为依赖走查。
- `utils/jwt.ts`（过期宽限）与 `utils/oauth.ts`（state 协议）暂无单测，以代码走查替代。
