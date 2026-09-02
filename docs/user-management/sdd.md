# 用户管理（User Management）设计规格（SDD）

> 关联：[SDD](./sdd.md) ｜ [BDD](./bdd.md) ｜ [TDD](./tdd.md) ｜ [SOP](./sop.md) ｜ 上游：[../auth/sdd.md](../auth/sdd.md)、[../account/sdd.md](../account/sdd.md) ｜ 证据：[local-dev/user-management-e2e.mjs](../../local-dev/user-management-e2e.mjs)
> 规范：[../DOC-STANDARD.md](../DOC-STANDARD.md)。行为场景见 BDD（@UM-Sxx），本文不重复。

## 目标

管理员治理用户：列表/详情、设撤管理员（root 专属）、禁用启用、重置密码。与自助能力（[../account/sdd.md](../account/sdd.md)）职责互斥。

## 角色与权限矩阵

| 角色 | 判定 | 
|---|---|
| root | `setting` 表 `key="root"` 的 value = 用户 id（种子即 admin，id=1） |
| admin | `user.IsAdmin==true` 或 root |

| 操作（BDD Feature） | root | admin | 目标保护（Handler 层） |
|---|---|---|---|
| 列表/详情 | ✅ | ✅ | — |
| 设/撤管理员 | ✅ | 403 | 非 root、非自己 → 400 |
| 禁用/启用 | ✅ | ✅ | 非 root、非自己 → 400；admin 不可操作其他 admin → 403 |
| 重置密码 | ✅ | ✅ | 非 root → 400；admin 不可重置其他 admin → 403 |

角色门禁在 Controller（`EnsureAdminAsync`/`EnsureRootAsync`，沿用 [../settings.md](../settings.md) 模式）；目标保护在 Handler（依赖 DB 事实）。

## 组件

```
src/account/
├── Shared/  QueryUserListCommand(PagedParamter+SearchText) / QueryUserInfoCommand
│            UpdateUserIsAdminCommand / UpdateUserIsDisableCommand / ResetUserPasswordCommand（均 IUserIdContext+IModelValidator）
├── Core/    Handlers/{UpdateUserIsAdmin,UpdateUserIsDisable,ResetUserPassword}CommandHandler
│            Queries/{QueryUserList,QueryUserInfo}CommandHandler
└── Api/     UserManageController（/usermanage）
ui/src/      api/usermanage.ts、pages/users/Users.tsx、路由 /users
```

## 关键决策

1. `isRoot` 由 `setting.key="root"` 实时计算（不可只信 IsAdmin，root 不可被降级）。
2. 列表 `OrderBy(Id)` 升序 + 服务端分页（PagedParamter 上限 1000）；SearchText 模糊匹配用户名/昵称/邮箱。
3. 重置密码复用注册管线：RSA(PKCS1) 解密 → 强度正则 → PBKDF2 加盐（见 [@UM-S24](./bdd.md#um-s24)）。
4. 写操作后必须 `RemoveUserStateAsync` 失效 Redis 用户态（`moai:userstate:{id}`，1h TTL）——禁用即时生效依赖此（见 [../account/sdd.md](../account/sdd.md) 缓存节）。
5. 前端 UI 按权限渲染：仅 isRoot 显示授权按钮；root 自己行不渲染危险操作（后端是最终防线）。
6. 列表页 UI 约定（2026-09-02 优化）：操作列为固定右侧（`fixed:'right'`）图标按钮，一律带 Tooltip 与 `aria-label`，危险操作保留 Popconfirm；表头 `sticky`（页面滚动时表头常驻）；首列为合并用户列（头像+用户名，昵称次行灰字、与用户名同名时省略）；时间统一 `YYYY-MM-DD HH:mm` 单行；管理页不渲染解释性副标题（Page 仅传 title）。

## 已知问题

- 修复史：路由回填 Command 被自动验证拦截恒 400（已按仓库惯例移除 UserId 规则）；禁用中间件 fail-open（已修，见 [../account/sdd.md](../account/sdd.md)）。
- 注册接口手机号重复返回裸 500（上游 [../auth/sdd.md](../auth/sdd.md) 已知问题）。
