# 前端账号设置页（AccountSettings）操作手册（SOP）

> 关联：[SDD](./sdd.md) ｜ [BDD](./bdd.md)（验收场景编号） ｜ [TDD](./tdd.md)（自动化映射） ｜ [SOP](./sop.md)

## 1. 用户自助操作

| 操作 | 步骤 | 对应场景 |
|---|---|---|
| 进入页面 | 侧边栏顶部头像 →「设置」（注意：侧边栏底部「设置」是管理员的系统设置页，入口不同） | [@FE-PA-S1](./bdd.md#fe-pa-s1) |
| 改昵称/手机号 | 基本设置卡编辑 → 保存；昵称必填，手机号为 5-20 位数字与 `+ - ( )` 空格组合 | [@FE-PA-S3](./bdd.md#fe-pa-s3)~[@FE-PA-S5](./bdd.md#fe-pa-s5) |
| 修改密码 | 旧密码 + 新密码（8-20 位且同时含字母和数字）+ 确认；密文传输；改密后当前登录态不失效，下次登录用新密码 | [@FE-PA-S6](./bdd.md#fe-pa-s6)~[@FE-PA-S9](./bdd.md#fe-pa-s9) |
| 更换头像 | 资料卡「上传头像」→ 选图片（仅图片类型，≤5MB），成功即生效 | [@FE-PA-S10](./bdd.md#fe-pa-s10)~[@FE-PA-S13](./bdd.md#fe-pa-s13) |
| 绑定/解绑第三方 | 第三方账号卡：「绑定」弹窗授权；「解绑」二次确认。绑定后可在登录页用对应图标登录 | [@FE-PA-S14](./bdd.md#fe-pa-s14)~[@FE-PA-S20](./bdd.md#fe-pa-s20) |

## 2. 开发者维护

1. **加表单卡**：`AccountSettings.tsx` 加 `<Card>` + 独立 `Form.useForm`；`api/account.ts` 加封装（空 catch 约定，错误交全局中间件）；后端新接口先 `npm run syncapi` 再封装；`account.*` 双语补词条。
2. **调头像限制**：`beforeUpload` 的类型判断与 5MB 上限 + 文案 `account.avatarTypeError/avatarSizeError`。
3. **历史偏差勿扩散**：本页 maxWidth 720 窄栏与带标题 `<Page>` 均为规范制定前的写法，新页面不要复制；手机号前端正则比后端宽，以后端为准（见 [SDD 已知问题](./sdd.md)）。

## 3. 排障

| 现象 | 原因 | 处理 |
|---|---|---|
| 保存昵称后侧边栏没变 | 档案刷新失败（网络/401） | 看全局错误提示；重登再试 |
| 上传头像一直转圈 | storage 直传失败（预签名地址过期/网络） | 重试；仍失败查后端 storage 配置 |
| 头像裂图 | serverInfo.serviceUrl 缓存过期 | 刷新页面重拉 serverinfo |
| 绑定弹窗无反应 | 渠道回调地址未含前端 `/oauth_login` | 检查第三方渠道配置（上游 [../../../docs/oauthconnect/sop.md](../../../docs/oauthconnect/sop.md)） |
| 绑定成功但列表未刷新 | 弹窗跨源，message 被忽略（[@FE-PA-S19](./bdd.md#fe-pa-s19)） | 手动刷新；确认回调页与前端同源部署 |
| 改密提示规则错误 | 新密码未同时含字母数字或长度不在 8-20 | 按提示调整 |

## 4. 验收流程（发布前）

前置：后端与至少一个 OAuth 渠道（可用本地模拟 OIDC）就绪。

1. `cd ui && npm run lint && npm run test && npm run typecheck` 全绿；
2. 走查全部场景（[@FE-PA-S1](./bdd.md#fe-pa-s1)~[@FE-PA-S20](./bdd.md#fe-pa-s20)）：资料三分支、改密四分支（含新旧密码登录验证）、头像四分支、绑定七分支；
3. 双语各走一遍主流程（en-US 无中文残留、布局不溢出）；暗色主题检查边框可读性。

## 5. 历史验收存档（L3 证据，保留原始记录）

- **2026-09-01 轮次 18 交付（as-built 回溯整理）**：全量回归 `npm run test` 13 文件 42 用例全绿、`npm run typecheck` 0 错误；手工走查主流程（资料修改含非法手机号拦截、改密强度/一致性/旧密码错误三分支、头像正常/非图片/超 5MB、绑定成功/失败回调与解绑）通过。
- **2026-09-01 第二轮全系统深度测试**（记录于 [../../../docs/user-management/sop.md](../../../docs/user-management/sop.md) 存档）：本页相关修复——oauthconnect PUT 恒 400（后端已修）、`update_userinfo` 超长昵称 500 → 校验拦截、avatar 伪造 objectKey → file 表校验 404；浏览器走查账号设置页通过。
- **2026-09-02 第三轮 OAuth 全链路 + 全页面回归**：本地模拟 OIDC Provider 完成绑定/解绑/注册/直通 12/12；全页面浏览器回归（含账号设置、成员视角）通过；最终 68/68+36/36+42/42、构建 0 错。

## 6. 变更记录

| 日期 | 变更 |
|---|---|
| 2026-09-01 | 功能交付（轮次 18，as-built） |
| 2026-09-02 | 按 [DOC-STANDARD](../../../docs/DOC-STANDARD.md) 重构：场景编号化（FE-PA）、四件互链、职责瘦身 |
