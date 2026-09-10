# 系统设置（Settings）操作手册（SOP）

> 关联：[SDD](./sdd.md) ｜ [BDD](./bdd.md)（验收场景编号） ｜ [TDD](./tdd.md)（自动化映射） ｜ [SOP](./sop.md)
> 设置项的**开发规范**（如何新增一个设置项）见 [../settings.md](../settings.md)。

## 1. 角色与内置项

| 角色 | 能力 | 对应场景 |
|---|---|---|
| root | 查看 + 修改设置项（页面唯一入口） | [@SET-S4](./bdd.md#set-s4)、[@SET-S13](./bdd.md#set-s13) |
| admin（非 root） | 接口 GET 可查，页面重定向 /dashboard；保存 403 | [@SET-S1](./bdd.md#set-s1)、[@SET-S9](./bdd.md#set-s9)、[@SET-S18](./bdd.md#set-s18) |
| member | 无（接口 403，页面重定向 /dashboard） | [@SET-S2](./bdd.md#set-s2)、[@SET-S15](./bdd.md#set-s15) |

内置项：知识图谱组 `OPEN_NEO4J`/`NEO4J_URI`/`NEO4J_USERNAME`/`NEO4J_PASSWORD`，默认 `"false"`/空（[@SET-S16](./bdd.md#set-s16)）。`key="root"` 为系统级配置，不在接口白名单内（[@SET-S6](./bdd.md#set-s6)），只能改库维护（谨慎）。root 判定与种子见 [SDD](./sdd.md)。

## 2. 知识图谱设置（Neo4j，仅 root）

1. root 登录前端 → 「系统设置」；非 root 的 admin 会被重定向到仪表盘（[@SET-S18](./bdd.md#set-s18)）。
2. 打开「开启 Neo4j 知识图谱」后填写连接地址、用户名、密码，点击「保存」（脏检查见 [@SET-S12](./bdd.md#set-s12)）：先写 `OPEN_NEO4J="true"`，再依次写三项连接设置（[@SET-S16](./bdd.md#set-s16)）。值无缓存，**下一个读请求即生效**。
3. 关闭开关再保存时仅写 `OPEN_NEO4J="false"`，不覆盖已有连接信息（[@SET-S17](./bdd.md#set-s17)）。
4. 业务侧读取统一走 `IKnowledgeGraphSettingsService`（[SDD](./sdd.md)），未开启时 `Enabled=false` 且不返回连接信息（[@SET-S19](./bdd.md#set-s19)）。

## 3. 排障

| 现象 | 原因 | 处理 |
|---|---|---|
| 保存 403「只有超级管理员可以修改设置项」 | 当前登录人非 root | 换 root 账号 |
| 保存 400「无效的配置项.」 | key 不在白名单（含 `root`） | 核对 key；新增设置项走 [../settings.md](../settings.md) 流程 |
| admin 访问设置页被重定向 / 看不到「知识图谱（Neo4j）」卡片 | 页面 root 专属，当前账号是 admin 但非 root | 换 root 账号（[@SET-S18](./bdd.md#set-s18)） |
| 新增设置项后页面不显示 | 前端固定字段渲染 | 同步改 `Settings.tsx` 与 i18n（见编写规范步骤 3） |
| root 变更后设置页权限未变 | `userstate:{userId}` 缓存最长 1h | 重登或等过期；授权操作会主动失效缓存 |

## 4. 验收流程（发布前）

1. 自动化：`node local-dev/audit-345.mjs`（覆盖 [@SET-S1](./bdd.md#set-s1)~[@SET-S4](./bdd.md#set-s4)、[@SET-S6](./bdd.md#set-s6)/[@SET-S7](./bdd.md#set-s7)；后端运行于 :5210）。
2. 手动走查：root（可配置知识图谱）与非 root（admin/member 访问设置页被重定向）两视角核对 [@SET-S11](./bdd.md#set-s11) ~ [@SET-S15](./bdd.md#set-s15)、[@SET-S18](./bdd.md#set-s18)。
3. 记录写入下「历史验收存档」。

## 5. 历史验收存档（L3 证据，保留原始记录）

- **2026-09-01 回溯初验**：按 SOP 内置 curl 脚本（root 登录 → GET /api/settings 200 含 oauth_auto_register → PUT true 200 回读 "true" → `not_exist_key`/`root` 均 400「无效的配置项.」→ member GET/PUT 均 403 → 未登录 401）对 `127.0.0.1:5210` 核对路由、门禁错误码与文案，与源码一致；该脚本后收敛为 [local-dev/audit-345.mjs](../../local-dev/audit-345.mjs)。
- **2026-09-02 第三轮**（存档 [../user-management/sop.md](../user-management/sop.md)）：OAuth 12/12 含「直通」分支（oauth_auto_register 开启自动建号）；浏览器全页面回归（设置页，root 视角）通过。
- 遗留观察：GET /api/settings 为 admin 专属（member 403，有意设计）。

## 6. 变更记录

| 日期 | 变更 |
|---|---|
| 2026-09-01 | 初版（回溯整理，目录 docs/settings/ 与编写规范 ../settings.md 区分）；同日按 [DOC-STANDARD](../DOC-STANDARD.md) 重构：场景编号化（@SET-S1~S15）、四件互链、职责瘦身 |
| 2026-09-10 | 新增知识图谱组 `OPEN_NEO4J`/`NEO4J_URI`/`NEO4J_USERNAME`/`NEO4J_PASSWORD`（@SET-S16~S19）、`IKnowledgeGraphSettingsService` 读取出口与前端 root 卡片 |
| 2026-09-10 | 移除 `oauth_auto_register`（无业务消费，@SET-S10 作废不复用）；系统设置页改为 root 专属，非 root 重定向，侧边栏设置入口仅 root 可见 |
