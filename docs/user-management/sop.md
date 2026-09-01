# 用户管理（User Management）操作手册（SOP）

> 关联：[SDD](./sdd.md) ｜ [BDD](./bdd.md)（验收场景编号） ｜ [TDD](./tdd.md)（自动化映射） ｜ [SOP](./sop.md)

## 1. 日常操作

| 操作 | 步骤 | 对应场景 |
|---|---|---|
| 授权新管理员（仅 root） | 用户页 → 目标行 →「设为管理员」→ 确认；对方重新登录生效 | [@UM-S7](./bdd.md#um-s7) |
| 撤销管理员（仅 root） | 同上 →「取消管理员」 | [@UM-S8](./bdd.md#um-s8) |
| 禁用账号 | 目标行 →「禁用」→ 二次确认；下一请求即拦截 | [@UM-S12](./bdd.md#um-s12) |
| 启用账号 | 目标行 →「启用」 | [@UM-S13](./bdd.md#um-s13) |
| 重置密码 | 目标行 →「重置密码」→ 合规新密码（8-20 位含字母+数字）→ **线下安全告知用户** | [@UM-S18](./bdd.md#um-s18) |

约束速查：root 不可被任何人操作（[@UM-S10](./bdd.md#um-s10)/[@UM-S16](./bdd.md#um-s16)/[@UM-S21](./bdd.md#um-s21)）；不能操作自己（[@UM-S11](./bdd.md#um-s11)/[@UM-S17](./bdd.md#um-s17)）；admin 之间互不可操作（[@UM-S14](./bdd.md#um-s14)/[@UM-S20](./bdd.md#um-s20)）。

## 2. 排障

| 现象 | 原因 | 处理 |
|---|---|---|
| 授权后对方菜单未变 | 前端 userinfo 未刷新 | 对方刷新/重登（用户态缓存已失效，仅前端状态滞后） |
| 禁用后对方仍在操作 | 请求发生在禁用前的同一会话窗口 | 下一请求必被拦，无需处理 |
| 授权接口 403 | 操作者非 root | 换 root（[@UM-S9](./bdd.md#um-s9)） |
| 重置密码 400 | 强度不足或目标受保护 | 按提示调整（[@UM-S19](./bdd.md#um-s19)/[@UM-S21](./bdd.md#um-s21)） |

## 3. 验收流程（发布前）

1. 自动化：跑 [TDD 回归命令](./tdd.md)（E2E 34/34 + vitest 3/3）。
2. 手动走查：admin 登录 http://localhost:4000/users，核对 [@UM-S22](./bdd.md#um-s22)、[@UM-S24](./bdd.md#um-s24)~[@UM-S26](./bdd.md#um-s26)。
3. 最近走查记录见下「历史验收存档」。
4. **给用户的图文操作手册**：[manual.html](./manual.html)（自包含单文件，含 11 张实测截图，可直接发给使用者）。

## 4. 历史验收存档（L3 证据，保留原始记录）

- **2026-09-01 交付验收**：E2E 34/34、Vitest 42/42、lint/typecheck 绿。修复：路由回填 Command 自动验证恒 400（移除 UserId 规则）；禁用中间件 fail-open（改为 403「账号已被禁用」）。
- **2026-09-01 第二轮·全系统深度测试**：深度 API 68/68（28 端点：注册校验矩阵、登录限流、token 边界、存储全链路、权限门禁、分页边界、CORS、畸形请求）；BDD 36/36；浏览器走查（登录→仪表盘→/users→禁用/启用）。修复 11 处缺陷，要点：publicStoreUrl /statics→/static 统一常量；注册手机号重复 500→409；oauthconnect PUT 恒 400→修复；垃圾 refreshToken 500→401；update_userinfo 超长昵称 500→IModelValidator；avatar 伪造 objectKey→file 表校验 404；oauthconnect 各 BusinessException 补 StatusCode；**前端环境**：`.env.development`(5000) 优先级高于 `.env.local`，新增 `ui/.env.development.local` 指向 5210。
- **2026-09-02 第三轮·OAuth 全链路 + 浏览器全页面回归**：本地模拟 OIDC Provider 完成 OAuth 12/12（创建/授权地址/302 回跳/待绑定/注册/直通/绑定/解绑）+ 浏览器全流程；修复第 12 缺陷（OAuthRegisterCommandHandler 占位手机号唯一索引冲突→sub 哈希生成）。全页面浏览器回归通过（注册/设置/oauthconnect 编辑/账号设置/成员视角）。最终回归 68/68+36/36+42/42、构建 0 错。
- 遗留观察：改密不吊销其他会话旧 token；GET /api/settings 为 admin 专属（member 403）。

## 5. 变更记录

| 日期 | 变更 |
|---|---|
| 2026-09-01 | 功能交付；三轮验收（见存档） |
| 2026-09-02 | 按 [DOC-STANDARD](../DOC-STANDARD.md) 重构：场景编号化、四件互链、职责瘦身 |
