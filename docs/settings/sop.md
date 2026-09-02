# 系统设置（Settings）操作手册（SOP）

> 关联：[SDD](./sdd.md) ｜ [BDD](./bdd.md)（验收场景编号） ｜ [TDD](./tdd.md)（自动化映射） ｜ [SOP](./sop.md)
> 设置项的**开发规范**（如何新增一个设置项）见 [../settings.md](../settings.md)。

## 1. 角色与内置项

| 角色 | 能力 | 对应场景 |
|---|---|---|
| root | 查看 + 修改全部设置项 | [@SET-S4](./bdd.md#set-s4) |
| admin | 仅查看（保存 403） | [@SET-S1](./bdd.md#set-s1)、[@SET-S9](./bdd.md#set-s9) |
| member | 无（接口 403，页面重定向 /dashboard） | [@SET-S2](./bdd.md#set-s2)、[@SET-S15](./bdd.md#set-s15) |

内置项：`oauth_auto_register`「允许第三方账号登录直接创建账号」，默认 `"false"`（业务效果 [@SET-S10](./bdd.md#set-s10)）。`key="root"` 为系统级配置，不在接口白名单内（[@SET-S6](./bdd.md#set-s6)），只能改库维护（谨慎）。root 判定与种子见 [SDD](./sdd.md)。

## 2. 修改设置项（仅 root）

1. root 登录前端 → 「系统设置」。
2. 切换目标开关 → 「保存」（脏检查见 [@SET-S12](./bdd.md#set-s12)）。值以 `"true"`/`"false"` 落库，无缓存，**下一个读请求即生效**。

## 3. 排障

| 现象 | 原因 | 处理 |
|---|---|---|
| 保存 403「只有超级管理员可以修改设置项」 | 当前登录人非 root | 换 root 账号 |
| 保存 400「无效的配置项.」 | key 不在白名单（含 `root`） | 核对 key；新增设置项走 [../settings.md](../settings.md) 流程 |
| admin 页面能切换开关但保存无效 | 后端拒绝 + 前端回滚 | 正常保护行为（[@SET-S14](./bdd.md#set-s14)） |
| 新增设置项后页面不显示 | 前端固定字段渲染 | 同步改 `Settings.tsx` 与 i18n（见编写规范步骤 3） |
| root 变更后设置页权限未变 | `userstate:{userId}` 缓存最长 1h | 重登或等过期；授权操作会主动失效缓存 |

## 4. 验收流程（发布前）

1. 自动化：`node local-dev/audit-345.mjs`（覆盖 [@SET-S1](./bdd.md#set-s1)~[@SET-S4](./bdd.md#set-s4)、[@SET-S6](./bdd.md#set-s6)/[@SET-S7](./bdd.md#set-s7)；后端运行于 :5210）。
2. 手动走查：root/admin/member 三视角核对 [@SET-S11](./bdd.md#set-s11) ~ [@SET-S15](./bdd.md#set-s15)；如具备 mock 第三方环境，验证 [@SET-S10](./bdd.md#set-s10) 直通。
3. 记录写入下「历史验收存档」。

## 5. 历史验收存档（L3 证据，保留原始记录）

- **2026-09-01 回溯初验**：按 SOP 内置 curl 脚本（root 登录 → GET /api/settings 200 含 oauth_auto_register → PUT true 200 回读 "true" → `not_exist_key`/`root` 均 400「无效的配置项.」→ member GET/PUT 均 403 → 未登录 401）对 `127.0.0.1:5210` 核对路由、门禁错误码与文案，与源码一致；该脚本后收敛为 [local-dev/audit-345.mjs](../../local-dev/audit-345.mjs)。
- **2026-09-02 第三轮**（存档 [../user-management/sop.md](../user-management/sop.md)）：OAuth 12/12 含「直通」分支（oauth_auto_register 开启自动建号）；浏览器全页面回归（设置页，root 视角）通过。
- 遗留观察：GET /api/settings 为 admin 专属（member 403，有意设计）。

## 6. 变更记录

| 日期 | 变更 |
|---|---|
| 2026-09-01 | 初版（回溯整理，目录 docs/settings/ 与编写规范 ../settings.md 区分）；同日按 [DOC-STANDARD](../DOC-STANDARD.md) 重构：场景编号化（@SET-S1~S15）、四件互链、职责瘦身 |
