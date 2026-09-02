# 账号自助（Account Self-Service）设计规格（SDD）

> 关联：[SDD](./sdd.md) ｜ [BDD](./bdd.md) ｜ [TDD](./tdd.md) ｜ [SOP](./sop.md) ｜ 上游：[../auth/sdd.md](../auth/sdd.md) ｜ 下游：[../user-management/sdd.md](../user-management/sdd.md)、[../oauthconnect/sdd.md](../oauthconnect/sdd.md) ｜ 证据：[local-dev/audit-345.mjs](../../local-dev/audit-345.mjs)
> 规范：[../DOC-STANDARD.md](../DOC-STANDARD.md)。行为场景见 BDD（@ACC-Sxx），本文不重复。

## 目标

为登录用户提供**自助**账号维护：查看/修改个人资料、自助改密、上传头像、绑定/解绑第三方账号。本模块只操作**当前登录用户自己**；管理员治理他人见 [../user-management/sdd.md](../user-management/sdd.md)（职责互斥）。所有接口要求登录态，无 admin/root 门禁——Handler 一律使用 `ContextUserId`，不信任请求体传入的用户 id（Command 实现 `IUserIdContext`）。

## 组件

```
src/account/
├── MoAI.Account.Shared/  Commands/{UpdateUserInfo,ResetPassword,UpdateUserAvatar,
│                         OAuthBindExistAccount,OAuthBindAccountByCode,UnbindUserOAuth}Command
│                         Queries/{QueryUserViewUserInfo,QueryUserBoundAccounts}Command + Responses
│                         Services/IUserAccountService
├── MoAI.Account.Core/    Handlers/ 六个 + Queries/ 两个；Services/{UserAccountService,
│                         CustomAuthorizaMiddleware,TokenProvider,UserContextProvider}
└── MoAI.Account.Api/     AccountController（[Route("/account")]，8 端点，/api 前缀）
ui/src/                   api/account.ts、pages/account/AccountSettings.tsx（/account）、
                         utils/{storage,oauth}.ts
```

## API 契约

| 方法 | 路由 | 说明 |
|---|---|---|
| GET | `/account/userinfo` | 返回 `UserStateInfo`（userId/userName/email/nickName/phone/avatar/isDisable/isAdmin/isRoot/isDeleted） |
| POST | `/account/update_userinfo` | `{nickName?, phone?}`；昵称非空白才覆盖，phone `null` 不动、传值（含空串）覆盖 |
| POST | `/account/reset_password` | `{oldPassword, newPassword}`（均 RSA 密文） |
| POST | `/account/avatar` | `{objectKey}`；须为 storage 已完成上传的对象键 |
| POST | `/account/oauth_bind` | `{oAuthId, code}`（code 模式绑定） |
| POST | `/account/oauth_bind_account` | `{tempOAuthBindId}`（临时 id 模式绑定） |
| POST | `/account/unbind_account` | `{providerId}` |
| GET | `/account/bound_accounts` | `BoundAccountInfo[]`（oAuthId/name/provider/iconUrl/createTime，联查 OauthConnections） |

`avatar` 由 `IStorageService.GetPublicFileUrl(AvatarPath)` 生成完整地址（`{serviceUrl}/static/{objectKey}`），未设置为空串。

## 关键决策

1. **用户态缓存**：`UserAccountService.GetUserStateAsync` 缓存 Redis `moai:userstate:{userId}`（TTL 1h），未命中查库回填；`IsRoot` 由 `setting.key="root"` 实时比对，`IsAdmin = user.IsAdmin || isRoot`；用户不存在返回 `IsDeleted=IsDisable=true` 占位。
2. **禁用拦截**：`CustomAuthorizaMiddleware` 对 `[Authorize]` 端点每请求查用户态，`IsDeleted || IsDisable` → 403「账号已被禁用」；查询异常保持放行（容错决策）。因此**写操作后必须 `RemoveUserStateAsync`**，否则前端 userinfo 最长滞后 1h。
3. **自助改密**：旧密文解密（异常同样 400「原密码错误」）→ PBKDF2 校验 → 新密码解密后强度正则（8-20 含字母数字）→ 重新加盐落库 → 失效缓存。
4. **头像只登记不传文件**：前端先走 storage 预上传（SHA-256 → `pre_upload_image` → 直传 OSS → `complate_url`），再 `POST /account/avatar` 登记 objectKey；Handler 校验 file 表存在（[@ACC-S12](./bdd.md#acc-s12)）。
5. **OAuth 绑定双模式**：code 模式经 `IOAuthUserProfileService` 换 sub；临时 id 模式读 Redis `oauth:bind:{guid}`（登录流程写入，10 分钟）。冲突规则：sub 已被他人绑定 400「第三方账号已被其它账号绑定」；绑定到本人幂等；同供应商已绑其它 sub 400「用户已绑定过其它账号」。
6. **解绑不失效用户态缓存**（缓存不含绑定信息）；按 `(ContextUserId, ProviderId)` 定位，无记录 404。
7. 前端：四张 Card（头像/资料/改密/第三方绑定）；绑定走 600×750 弹窗（state 附加 `:bind`），`postMessage`（同源校验）回传结果；错误提示交全局中间件。

## 已知问题

- **改密/重置后不吊销其他会话的存量 token**：旧 Access Token 在剩余有效期内仍可用（无黑名单机制，全局遗留观察，见 [../auth/sdd.md](../auth/sdd.md)）。
- OAuth 绑定成功链路依赖真实第三方授权码，自动化需 mock Provider（环境依赖）。
- 修复史（均已回归）：
  - 禁用中间件曾整体 fail-open（禁用不生效），已修复为按用户态拦截；
  - `update_userinfo` 超长昵称曾 500，已加 IModelValidator 校验；
  - `avatar` 伪造 objectKey 曾通过登记，已加 file 表校验（404「头像文件不存在或未完成上传.」）。
