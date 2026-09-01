# 前端账号设置页（AccountSettings）设计规格（SDD）

> 关联：[SDD](./sdd.md) ｜ [BDD](./bdd.md) ｜ [TDD](./tdd.md) ｜ [SOP](./sop.md) ｜ 上游：[../../../docs/account/sdd.md](../../../docs/account/sdd.md)（自助接口契约）、[../../../docs/storage/sdd.md](../../../docs/storage/sdd.md)（头像直传管线）、[../store-i18n/sdd.md](../store-i18n/sdd.md)（userInfo/serverInfo） ｜ 证据：`cd ui && npm run test`（回归）+ 手工走查
> 规范：[../../../docs/DOC-STANDARD.md](../../../docs/DOC-STANDARD.md)。行为场景见 BDD（@FE-PA-Sxx），本文不重复。

## 目标

登录用户的自助管理入口：修改昵称/手机号、修改密码、上传头像、绑定/解绑第三方登录。路由 `/account`（`RequireAuth + AppLayout` 受保护子路由）；入口为侧边栏头像 Dropdown「设置」。单文件页面 `AccountSettings.tsx` + API 封装 `api/account.ts`；i18n 前缀 `account.*`（zh-CN/en-US 各 30 键）。

## 组件与页面结构

居中窄栏（maxWidth 720）内四个 Card，自上而下：

1. **资料卡**：72px 头像（`resolveStorageUrl` 解析 osskey；无头像回退图标/昵称首字符）+ 昵称/邮箱展示 + 「上传头像」按钮（antd Upload 的 `beforeUpload` 前置校验 + `customRequest` 自定义上传）。
2. **基本资料表单**：`nickName` 必填（maxLength 50）；`phone` 可选，前端正则 `^[\d+\-()\s]{5,20}$`；提交后 `refreshUserProfile()` 合并刷新 store，侧边栏同步。
3. **修改密码表单**：旧密码 + 新密码（前端强度正则 `(?=.*[A-Za-z])(?=.*\d)[A-Za-z\d\S]{8,20}`，与后端同规则）+ 确认密码（一致性校验）；新旧密码均在浏览器以服务器 RSA 公钥加密后提交（见 [../../../docs/auth/sdd.md](../../../docs/auth/sdd.md) 加密通道）。
4. **第三方账号绑定卡**：`Promise.all([getOAuthProviders(), getBoundAccounts()])` 加载；每提供商一行（图标/名称/绑定状态 + 绑定或解绑按钮）；绑定走授权弹窗（state 追加 `:bind` 标志，600x750），绑定动作在回调页 `OAuthLogin` 完成，本页经同源 `postMessage` 收结果；解绑走 `Popconfirm` 二次确认。

## 头像上传管线（storage 直传）

```
beforeUpload：非 image/* 或 >5MB → 就地报错并取消上传
customRequest → uploadImageWithKey（utils/storage.ts）
  ① SHA-256 摘要 ② 预上传申请（返回 objectKey/预签名地址/秒传标记）
  ③ 非秒传：PUT 预签名地址直传 → 完成回调 ④ POST /account/avatar 只登记 objectKey
→ refreshUserProfile() 头像即时刷新
```

文件字节走 storage 预签名直传，account 接口只存 objectKey；展示统一由 `resolveStorageUrl` 解析。契约见 [../../../docs/storage/sdd.md](../../../docs/storage/sdd.md)。

## API 契约（`ui/src/api/account.ts`）

| 函数 | HTTP | 说明 |
|---|---|---|
| `getBoundAccounts()` | `GET /account/bound_accounts` | 绑定列表（oAuthId/name/provider/iconUrl/createTime） |
| `updateUserInfo(p)` | `POST /account/update_userinfo` | `{ nickName?, phone? }` |
| `resetPassword(old, new)` | `POST /account/reset_password` | 两个密码均 RSA 密文（内部取 serverInfo 公钥） |
| `uploadAvatar(file)` | `POST /account/avatar` | `{ objectKey }`，返回展示 url |
| `unbindProvider(id)` | `POST /account/unbind_account` | `{ providerId }` |
| `oauthBindByCode(p)` | `POST /account/oauth_bind` | 回调页 OAuthLogin 弹窗用，本页不直接调用 |

全部经 `getApiClient()`（Bearer；401 由全局中间件清态跳登录）；页面 catch 留空，错误提示统一由全局 `feedback.handleError` 承担。

## 关键决策

1. 改密/资料/头像均先前端校验后提交，后端仍是最终裁决（[../../../docs/account/sdd.md](../../../docs/account/sdd.md)）。
2. 绑定弹窗回调只信同源 message（[@FE-PA-S19](./bdd.md#fe-pa-s19)）。
3. 表单回填由 `useEffect` 监听 store 中 `nickName/phone` 变化驱动。

## 已知问题

- **与 frontend-conventions.md 的两处历史偏差**（规范后定，页面未回改，新页面勿模仿）：容器 `maxWidth: 720` 居中窄栏（现行规范要求内容适配不加 maxWidth 上限）；`<Page>` 传了 title/subtitle（现行规范要求设置类页面用无标题 `<Page>`）。
- 页面**无组件测试**（`ui/src/pages/account/` 下无 `__tests__`），行为靠手工走查（见 [TDD](./tdd.md)）。
- 手机号前端正则（`^[\d+\-()\s]{5,20}$`）与后端注册校验（`^(?:\+?1)?\d{10,15}$`）**宽严不一**，以后端裁决为准。
- 头像 5MB/类型限制是纯前端拦截；解绑是否要求至少保留一种登录方式未做前端前置检查，以后端响应为准。
- 密码强度前端校验仅为体验前置，后端解密后按同一正则最终裁决。
