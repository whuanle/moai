# 前端布局导航与路由（layout-routing）验证映射（TDD）

> 关联：[SDD](./sdd.md) ｜ [BDD](./bdd.md)（场景定义） ｜ [TDD](./tdd.md) ｜ [SOP](./sop.md)
> 本文只做「场景 → 验证物 → 结果」映射；场景语义见 BDD 编号，设计依据见 SDD。本模块组件（AppLayout/AppSider/RequireAuth/router）无直接单测，验证 = 代码走查 + 消费方单测 + dev server 实测。

## 场景映射表

| 场景 | 验证物 | 结果（日期） |
|---|---|---|
| @FE-LR-S18 | [ui/src/pages/users/__tests__/Users.test.tsx](../../src/pages/users/__tests__/Users.test.tsx)（消费方，含「非管理员访问重定向到 dashboard」用例） | PASS 3/3（2026-09-01） |
| @FE-LR-S5 ~ @FE-LR-S7 | curl 路由 HTTP 200（dev server :5199，Vite SPA 回退 index.html）：/login /register /oauth_login /design-system /dashboard /account /users /settings /oauthconnect /app 共 10 路由 | PASS 10/10（2026-09-01） |
| @FE-LR-S1 ~ @FE-LR-S4 | @manual 代码走查：RequireAuth.tsx（无 token 同步 Navigate / mount 一次 + 60s 周期 / 失败清态跳转 / Spin）+ api/kiota.ts 401 拦截；认证语义见 [../../docs/auth-flow/](../../docs/auth-flow/tdd.md) | PASS（2026-09-01） |
| @FE-LR-S6 兜底 | @manual 走查：router `Navigate to="/dashboard" replace` ×2（index 与 *）；pathToKey 含 /app /wiki /team /plugin 四键而路由表缺对应项（成因见 [SDD 已知问题 1](./sdd.md)） | PASS（2026-09-01） |
| @FE-LR-S8 ~ @FE-LR-S17 | @manual 浏览器走查（[SOP 第 4 节](./sop.md)） | PASS（2026-09-01，见 SOP 存档） |
| @FE-LR-S22 | @manual 浏览器实测（dev :4000）：/team/1、/team/1/apps、/team/1/knowledge、/team/1/app/:id（应用设计）、/team/1/app/:id/chat（应用对话）均无一级侧边栏（展开/收起按钮 0 个），页面二级导航与返回入口正常 | PASS 5/5（2026-09-17） |
| @FE-LR-S23 | @manual 浏览器实测：/team 列表与 /dashboard 下一级侧边栏恢复展开（收起菜单按钮存在） | PASS（2026-09-17） |

## 回归命令

```bash
cd ui && npx vitest run src/pages/users     # 消费方：1 file / 3 tests
cd ui && npm run test -- --run              # 全量基线：13 files / 42 tests
cd ui && npm run lint && npm run typecheck
# 路由实测：npm run dev -- --port 5199 后
# for p in /login /register /oauth_login /design-system /dashboard /account /users /settings /oauthconnect /app; do curl -s -o /dev/null -w "%{http_code} $p\n" http://localhost:5199$p; done
```

## 覆盖率说明

- 23 个场景中 1 个由消费方单测自动化、3 个由 curl 实测覆盖、二级页侧边栏隐藏/恢复（S22~S23）由浏览器实测覆盖，其余为走查型（布局/守卫组件无直接单测）。
- SPA 回退返回 200 只证明路由可达，登录态行为仍需按 SOP 手工走查（未登录跳 /login、非管理员回 /dashboard）。
