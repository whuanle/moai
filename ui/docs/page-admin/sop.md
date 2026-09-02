# 前端管理页（Settings / OauthConnect）操作手册（SOP）

> 关联：[SDD](./sdd.md) ｜ [BDD](./bdd.md)（验收场景编号） ｜ [TDD](./tdd.md)（自动化映射） ｜ [SOP](./sop.md)

## 1. 角色与入口

| 操作 | 谁能做 | 入口 | 场景 |
|---|---|---|---|
| 系统设置（自动注册开关） | admin+ | 侧边栏管理组「设置」 | [@FE-PG-S7](./bdd.md#fe-pg-s7)~[@FE-PG-S9](./bdd.md#fe-pg-s9) |
| 第三方登录渠道维护 | admin+ | 侧边栏管理组「第三方登录」 | [@FE-PG-S10](./bdd.md#fe-pg-s10)~[@FE-PG-S16](./bdd.md#fe-pg-s16) |
| 普通用户 | 无 | 菜单不可见；直访被重定向；接口 403 | [@FE-PG-S2](./bdd.md#fe-pg-s2)/[@FE-PG-S4](./bdd.md#fe-pg-s4)~[@FE-PG-S6](./bdd.md#fe-pg-s6) |

> 管理组「插件」（/plugin）为占位导航，点击回概览（[@FE-PG-S3](./bdd.md#fe-pg-s3)）；「用户」见 [../../../docs/user-management/sop.md](../../../docs/user-management/sop.md)。

## 2. 配置「第三方登录自动注册」

1. `/settings` → 基本设置卡切换开关 → 保存（未修改前保存不可点；保存失败自动回滚为库中真值，[@FE-PG-S9](./bdd.md#fe-pg-s9)）。
2. **开**：第三方登录遇未注册用户自动建号；**关**：走注册确认/绑定流程（上游 [../../../docs/auth/sop.md](../../../docs/auth/sop.md)）。
3. 等价运维直改：`setting` 表 `key='oauth_auto_register'`，`value='true'|'false'`（前端按字符串比较）。

## 3. 维护第三方登录渠道

1. **新建自定义 OIDC**：类型 Custom OAuth + 名称/Key/Secret/图标 + 发现端点（如 `https://sso.example.com/.well-known/openid-configuration`）；保存后「授权地址」列可复制，回调地址固定为 `{WebUI}/oauth_login`。
2. **飞书/钉钉**：表单不出现发现端点；图标留空自动填官方图标；Key/Secret 对应开放平台凭据（钉钉 corp 参数在后端配置）。
3. **编辑**：可改名称/Key/图标/发现端点；类型不可改；Secret 留空表示不变（[@FE-PG-S15](./bdd.md#fe-pg-s15)）。
4. **删除**：行内「删除」→ 二次确认，立即生效。

## 4. 排障

| 现象 | 原因 | 处理 |
|---|---|---|
| 侧边栏没有管理组 | 当前登录人非 admin | 由 root 在用户页授权后重登 |
| 保存设置后开关跳回 | 保存失败触发页面回滚 | 看全局错误提示（网络/权限） |
| 新建渠道 400 | 必填项缺失 | 补全 name/key/secret/iconUrl（custom 还需发现端点） |
| 编辑渠道返回 400 | 历史后端缺陷（**已修复**，2026-09-02 实测 200） | 若复现回查后端 `UpdateOAuthConnectionCommand.Validate`（上游排障表） |
| 登录页图标裂图 | iconUrl 为对象键且 serverInfo 缓存过期 | 刷新页面；检查 serverinfo |
| 渠道删除后登录页仍显示 | 前端进登录页实时拉取 | 刷新登录页 |

## 5. 验收流程（发布前）

前置：root + 一个普通账号；后端已起。

1. `cd ui && npm run lint && npm run test && npm run typecheck` 全绿；
2. 双角色走查：member 无管理组、直访两页被重定向（[@FE-PG-S2](./bdd.md#fe-pg-s2)/[@FE-PG-S4](./bdd.md#fe-pg-s4)/[@FE-PG-S5](./bdd.md#fe-pg-s5)）；admin 管理组四入口可见、「插件」回概览（[@FE-PG-S3](./bdd.md#fe-pg-s3)）；
3. Settings：改开关保存成功（[@FE-PG-S8](./bdd.md#fe-pg-s8)）；模拟断网保存 → 回滚（[@FE-PG-S9](./bdd.md#fe-pg-s9)）；
4. OauthConnect：新建 custom 渠道（含图标上传）→ 登录页出现新图标（[@FE-PG-S11](./bdd.md#fe-pg-s11)）；编辑不改 Secret 提交成功（[@FE-PG-S15](./bdd.md#fe-pg-s15)）；删除后图标消失（[@FE-PG-S16](./bdd.md#fe-pg-s16)）。

## 6. 历史验收存档（L3 证据，保留原始记录）

- **2026-09-01 轮次 19 交付（as-built 回溯整理）**：`npm run test` 13 文件 42 用例全绿、`npm run typecheck` 0 错误；admin/member 双角色走查通过。
- **2026-09-01 缺陷记录**：编辑渠道 `PUT /api/oauthconnect/connections/{id}` 被后端 SharpGrip 自动验证拦截（路由回填 Command 的 `OAuthConnectionId NotEmpty` 规则）恒 400，当时「编辑」不可用，以删除重建绕行（同款问题在 usermanage 三接口已先修复）。
- **2026-09-01 第二轮全系统深度测试**：修复 oauthconnect PUT 恒 400 与各 BusinessException 补 StatusCode（记录于 [../../../docs/user-management/sop.md](../../../docs/user-management/sop.md) 存档）；浏览器走查 oauthconnect 编辑通过。
- **2026-09-02 终审实测**：**后端 PUT 缺陷确认已修复**（Validate 移除路由回填字段规则），实测编辑提交返回 200，前端编辑功能恢复可用（与上游 [../../../docs/oauthconnect/sop.md](../../../docs/oauthconnect/sop.md) 排障表同源）。

## 7. 变更记录

| 日期 | 变更 |
|---|---|
| 2026-09-01 | 功能交付（轮次 19，as-built）；记录编辑接口缺陷与 /plugin 占位导航 |
| 2026-09-02 | 后端 PUT 缺陷修复实测 200；按 [DOC-STANDARD](../../../docs/DOC-STANDARD.md) 重构：场景编号化（FE-PG）、四件互链 |
