# 认证（Auth）设计规格（SDD）

> 关联：[SDD](./sdd.md) ｜ [BDD](./bdd.md) ｜ [TDD](./tdd.md) ｜ [SOP](./sop.md) ｜ 上游：无（入口模块） ｜ 下游：[../account/sdd.md](../account/sdd.md)、[../user-management/sdd.md](../user-management/sdd.md)、[../oauthconnect/sdd.md](../oauthconnect/sdd.md) ｜ 证据：[local-dev/auth-lockout-check.mjs](../../local-dev/auth-lockout-check.mjs)、[local-dev/user-management-e2e.mjs](../../local-dev/user-management-e2e.mjs)
> 规范：[../DOC-STANDARD.md](../DOC-STANDARD.md)。行为场景见 BDD（@AUTH-Sxx），本文不重复。

## 目标

系统身份认证入口：账号密码注册/登录、JWT 签发与刷新、第三方 OAuth 登录/一键注册。auth 模块（`src/auth`，Shared/Core/Api 三层，见 [../cqrs-conventions.md](../cqrs-conventions.md)）负责认证编排；密码安全与 token 构建依赖基础设施。

## 认证机制

| 机制 | 设计 |
|---|---|
| 密码传输 | 前端 `GET /common/serverinfo` 取 `rsaPublic`（`IRsaProvider`，Base64 DER），RSA PKCS1 加密上报，服务端解密（`ui/src/utils/rsa.ts`）。同一 RSA 密钥兼用于 JWT 签名 |
| 密码存储 | PBKDF2（`src/infra/MoAI.Infra.Shared/Helpers/PBKDF2Helper.cs`）：迭代 10000、SHA256、salt 128 字节、输出 128 字节，哈希与 salt Base64 落库 |
| Access Token | RS256；Issuer/Audience = `SystemOptions.Server`；claims 含 `sub/name/nickname/email/jti/typ` + `Properties`（角色）；有效期 Release 30 分钟、DEBUG 条件编译 7 天 |
| Refresh Token | 仅 `sub/jti/token_type=refresh_token`；7 天；`/auth/refresh_token` 强制校验类型（[@AUTH-S10](./bdd.md#auth-s10)） |
| 登录防爆破 | Redis `moai:login:fail:{userName}`（库默认 `moai:` 前缀），失败 INCR + 5 分钟 TTL，≥5 次 403；成功清零（[@AUTH-S3](./bdd.md#auth-s3)） |
| 用户态缓存 | `IUserAccountService`（`src/account`，Redis 1h）供鉴权中间件读取；OAuth 注册改写用户名后失效 |
| 角色注入 | root = `setting.key="root"` 的 value=用户 id；否则 `IsAdmin==true` → admin。随 `Properties` 写入 token，业务判权走用户态缓存不重复解 JWT |

## 组件

```
src/auth/
├── MoAI.Auth.Shared/   Commands/{Login,RegisterUser,SendEmailCode,ResetPasswordByEmail,OAuthLogin,OAuthRegister,RefreshToken}Command + Responses
│                       Queries/{QueryRepeatedUserName,QueryAllOAuthPrivider}Command
│                       Services/{ITokenProvider,IOAuthUserProfileService,IEmailCodeService}、Models/{OAuthBindUserProfile,EmailCodePurpose}
├── MoAI.Auth.Core/     Handlers/ 七个 CommandHandler + Queries/ 两个 QueryHandler
│                       Services/{OAuthUserProfileService,EmailCodeService（验证码生成/Redis 存储/模板发送）,EmailCodeTemplate（SmartFormat 白名单插值）}
└── MoAI.Auth.Api/      AuthController（[Route("/auth")]，全部 [AllowAnonymous]，部署加 /api 前缀）
src/account/            TokenProvider（RS256 签发/校验）
ui/src/                 api/auth.ts、pages/auth/{Login,Register,ForgotPassword,OAuthLogin}.tsx、utils/{rsa,jwt}.ts
```

## API 契约

| 方法 | 路由 | 说明 |
|---|---|---|
| POST | `/auth/login` | userName（用户名或邮箱）+ RSA 密文密码 → token 对 |
| POST | `/auth/register` | 五字段（+可选 emailCode）→ 新用户 id；Handler 解密后校验强度（`(?=.*[A-Za-z])(?=.*\d)[A-Za-z\d\S]{8,20}$`）；注册验证码开启时先校验邮箱验证码 |
| POST | `/auth/email_code/send` | purpose（register/resetPassword）+ email → 发送验证码；注册用途需 SMTP+开关双开启且邮箱未占用（409），找回用途需 SMTP 启用（未注册 404） |
| POST | `/auth/reset_password` | email + 6 位验证码 + RSA 新密码 → 重置密码；需 SMTP 邮件服务启用 |
| POST | `/auth/oauth_login` | code + oAuthId → 已绑定直通 / 未绑定返回 `tempOAuthBindId` |
| POST | `/auth/oauth_register` | tempOAuthBindId → 一键注册并登录 |
| GET | `/auth/oauth_prividers` | 匿名；可选 `redirectUrl`（登录页来源），回调地址跟随该来源，未携带回退 `WebUI`；返回各提供商拼好的授权地址 |
| POST | `/auth/refresh_token` | 仅 refresh_token 类型可用 |

响应要点：`expiresIn` 为 **Access Token 过期时刻的 Unix 毫秒时间戳**（注释写"秒"，实现是毫秒，前端按毫秒解析）；`tokenType` 固定 `Bearer`。

## 关键决策

1. 密码强度在 Handler 解密后校验（DTO 只 NotEmpty）——密文无法预校验；解密失败同样 400。
2. 重复注册检查用户名/邮箱/手机号三者任一命中即 409（提示具体字段）。
3. OAuth 未绑定 profile 写 Redis `oauth:bind:{guid}`（10 分钟 TTL）；一键注册在 `TransactionScope` 内以占位 Guid 建号、改为 `u{自增id}`、失效用户态缓存、写 `UserOauthConnections`（ProviderId+Sub）。
4. 授权地址按提供商特殊拼接：默认 `scope=openid profile`、飞书 scope 为空、钉钉 `openid corpid`+`prompt=consent`、企业微信 `login_type=CorpApp&appid={CorpID}&agentid={AgentId}`；`state={OAuthId}`；`redirect_uri` **跟随登录页来源**（前端把 `window.location.origin` 作为 `redirectUrl` 传入 `oauth_prividers`/`oauth_login`/账号绑定接口，仅接受 http/https，未携带回退 `WebUI`）——内网穿透测试前端域名与 WebUI 配置不同时零后端配置；提供方后台的回调域白名单是安全边界。飞书换 token 的 `redirect_uri` 必须与授权时**逐字符一致**（两步均拼 `{来源}/oauth_login`，授权地址不得附加其他参数，2026-10-27 修复不一致缺陷），故同源透传到 `GetProfileAsync`。企业微信换身份链路（`IWeixinWorkClient`）：gettoken(corpid+secret) → getuserinfo(code)→userid → user/get 尽力补姓名头像（通讯录无权限/失败回退 userid，不阻断）。
5. 前端：401 且非 login 接口 → 清用户态跳 `/login`（`kiota.ts` FilterRequestHandler）；Access Token 过期（60 秒宽限）且 Refresh 有效时静默续期；OAuthLogin 用 `useRef` 防 StrictMode 重复消费一次性 code。
6. **所有内部签发 token 的处理器必须显式设 `UserType = Normal`**（Login/Refresh/OAuthLogin/OAuthRegister）：claim `typ` 缺失会解析为 None，下游 `app_agent_session.user_type` 落 0，对话日志把内部用户误标为外部用户（2026-09-20 修复 Refresh/OAuth 两类三处漏设，回归 [@AUTH-S24](./bdd.md#auth-s24)；存量数据见 `asserts/app_agent_session_user_type_fix.sql`）。
7. **邮箱验证码**（[@AUTH-S28](./bdd.md#auth-s28) ~ [@AUTH-S32](./bdd.md#auth-s32)）：6 位数字（`RandomNumberGenerator` CSPRNG），Redis `emailcode:{purpose}:{email}`（10 分钟 TTL）+ `emailcode:cd:*`（60 秒发送冷却），错 5 次作废；邮件发送成功才落验证码，失败可立即重试。找回密码可用性**仅取决于 SMTP 邮件服务是否启用**（未启用由管理员在用户管理重置），注册验证码需 SMTP + `REGISTER_EMAIL_CODE_ENABLED` 双条件；两者经 `serverinfo` 匿名回显给前端渲染开关。邮件主题/正文模板存于系统设置，`EmailCodeTemplate` 以 SmartFormat 白名单插值（仅 `{code}/{siteName}/{expireMinutes}`，其余花括号字面保留，策略同 variable 模块 `TeamVariableTemplate`；auth 为底座层不反向依赖 variable，独立实现）；模板留空回退内置默认。auth 模块因此新增对 `MoAI.Settings.Shared` 的项目引用（只用接口，实现注册在 SettingsCoreModule）。

## 已知问题

- **oauth_prividers 回跳地址死代码（2026-10-27 已修复并重新设计）**：原 Controller 空参构造导致 `RedirectUrl` 校验永不触发（[@AUTH-S14](./bdd.md#auth-s14) 记录为缺陷）。现修复为：Controller `[FromQuery]` 绑定并回填，校验收紧为仅接受 http/https 绝对地址（400），语义从「必须同 WebUI host」改为「跟随登录页来源」，支持内网穿透场景零后端配置。
- `LoginCommandHandler` 内有大段注释掉的 OAuth 绑定旧逻辑（历史遗留，待清理）。
- DEBUG 编译 Access Token 7 天与 Release 30 分钟不一致，本地联调易误判"token 不过期"。
- 修复史：注册手机号重复曾返回裸 500（唯一索引异常未转译），已修复为 409「手机号 {0} 已被注册」（源码 `RegisterUserCommandHandler` 现行实现已含三者检查）。
