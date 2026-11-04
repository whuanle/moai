# 认证（Auth）验证映射（TDD）

> 关联：[SDD](./sdd.md) ｜ [BDD](./bdd.md)（场景定义） ｜ [TDD](./tdd.md) ｜ [SOP](./sop.md)
> 本文只做「场景 → 验证物 → 结果」映射；场景语义见 BDD 编号，设计依据见 SDD。

## 场景映射表

| 场景 | 验证物 | 结果（日期） |
|---|---|---|
| @AUTH-S1、@AUTH-S2 | [local-dev/user-management-e2e.mjs](../../local-dev/user-management-e2e.mjs)（登录正负向） | PASS 34/34（2026-09-02） |
| @AUTH-S3 | [local-dev/auth-lockout-check.mjs](../../local-dev/auth-lockout-check.mjs)（5 错→403、删 key→恢复 200） | PASS（2026-09-01） |
| @AUTH-S5 | [local-dev/user-management-e2e.mjs](../../local-dev/user-management-e2e.mjs)（注册正向） | PASS 34/34（2026-09-02） |
| @AUTH-S6、@AUTH-S7 | 同上脚本 + 补充自检（弱密码 400、重复 409；手机号 409 为修复后回归，见 [../user-management/sop.md](../user-management/sop.md) 第二轮 68/68） | PASS（2026-09-01/09-02） |
| @AUTH-S8 | 深度回归「注册校验矩阵」（存档 [../user-management/sop.md](../user-management/sop.md) 第二轮） | PASS 68/68（2026-09-01） |
| @AUTH-S9、@AUTH-S10 | [local-dev/user-management-e2e.mjs](../../local-dev/user-management-e2e.mjs)（旧密码失败/新密码成功 + access 冒充用例） | PASS（2026-09-01） |
| @AUTH-S24 | [local-dev/user-management-e2e.mjs](../../local-dev/user-management-e2e.mjs)（UM-36a~c：登录/刷新 token typ=normal 且 sub 一致；回归刷新链路漏设 UserType 导致内部用户被误标外部） | PASS 37/37（2026-09-20） |
| @AUTH-S4、@AUTH-S11、@AUTH-S12、@AUTH-S18 | @manual 代码走查（Login/RefreshToken Handler 分支；refresh TTL>7 天与 oauth:bind 过期路径） | PASS（2026-09-01） |
| @AUTH-S13、@AUTH-S15 ~ @AUTH-S17、@AUTH-S19 | OAuth 全链路 12/12（本地 mock OIDC Provider，存档 [../user-management/sop.md](../user-management/sop.md) 第三轮） | PASS 12/12（2026-09-02） |
| @AUTH-S25 | URL 拼接部分见 [local-dev/oauth-weixinwork-smoke.mjs](../../local-dev/oauth-weixinwork-smoke.mjs)（login_type/appid/agentid 断言）；真实换码链路 @manual（需企业微信真实应用凭据） | PASS 17/17（URL 部分，2026-10-27） |
| @AUTH-S14 | [local-dev/oauth-weixinwork-smoke.mjs](../../local-dev/oauth-weixinwork-smoke.mjs)（穿透来源跟随 + ftp 协议 400） | PASS 20/20（2026-10-27，死代码已修复并重设计为跟随登录页来源） |
| @AUTH-S13、@AUTH-S14 | 缺陷修复回归：授权 `redirect_uri` 误拼 `?redirectUri=` 后缀与飞书换 token 不一致（登录/绑定均报「第三方接口错误」，根因见 [SOP 排障](./sop.md)）；删后缀 + 删伪 PKCE `code_verifier`；URL 级断言（origin/pathname）与冒烟脚本兼容，真实飞书换 token @manual（重启后端后浏览器复测） | build 0 error（2026-10-27） |
| @AUTH-S20 ~ @AUTH-S23 | @manual 浏览器走查（[SOP 第 4 节](./sop.md)；轮 11 独立验证 auth 前端链路） | PASS（2026-09-01/09-02） |
| @AUTH-S26 ~ @AUTH-S35 | [local-dev/auth-email-code-e2e.mjs](../../local-dev/auth-email-code-e2e.mjs)（34 断言：serverinfo 双开关回显/未启用拒绝/未注册 404/发码与 60 秒冷却 429/缺码·错码 400/一次性与错 5 次作废/默认与自定义模板插值/重置后旧密码 401 新密码登录/关闭 SMTP 全链路拒绝；内置 SMTP 桩，快照恢复设置） | PASS 34/34（2026-10-29） |
| @AUTH-S33 ~ @AUTH-S35（前端部分） | `ui/src/pages/auth/__tests__/Register.test.tsx`（验证码区按开关显隐 4/4）、`ui/src/pages/auth/__tests__/ForgotPassword.test.tsx`（未启用提示禁用/启用完整表单 3/3） | PASS 7/7（2026-10-29） |
| @AUTH-S36 ~ @AUTH-S42 | [local-dev/auth-2fa-e2e.mjs](../../local-dev/auth-2fa-e2e.mjs)（策略基线/TOTP 绑定全流程与错码/登录挑战一次性票据与防重放/关闭/强制 2FA 受限 token 与白名单与旧 token 立即恢复/强制期间禁关/root 重置与目标保护/邮箱验证码三模式；原 54 断言含 S42 两段式，S42 已于 2026-11-03 退役拆分至 S43~S46） | PASS 54/54（2026-11-01，S42 部分已退役） |
| @AUTH-S37（算法层） | `tests/MoAI.Infra.Tests/TotpHelperTests.cs`（RFC 6238 附录 B 向量 6 例 + 窗口容差/非法输入/base32 往返/otpauth URI 转义 10 例） | PASS 10/10（2026-11-01） |
| @AUTH-S43 ~ @AUTH-S44、@AUTH-S46 | [local-dev/auth-2fa-e2e.mjs](../../local-dev/auth-2fa-e2e.mjs)（67 断言：设置校验含 SET-92/一体化邮箱验证码三字段登录·challenge_policy 回显·发码防枚举静默·冷却 429·缺码错码 400/TOTP 优先不发邮件/SMTP 未启用失效；脚本内置 SMTP 桩与纯 node RFC 6238 端对拍 + RESP 直连 Redis 读图形验证码答案，快照恢复设置） | PASS 67/67（2026-11-03） |
| @AUTH-S45 | 同上脚本分级递进段（图形码先行/策略命中发码需过码/一码一登录/失败计数保留与清零时机） | PASS 67/67（2026-11-03） |
| @AUTH-S48 ~ @AUTH-S53 | [local-dev/auth-captcha-e2e.mjs](../../local-dev/auth-captcha-e2e.mjs)（40 断言：captcha/new 结构与 SVG 自绘/三模式 policy 回显/同码错 3 次作废/不区分大小写/一码一登录/图形码错误不计登录失败/阈值 3 分级递进/注册与找回发码面首发免费再发过码且只验不消费/限流 429；前置 E2E_REDIS 指向 Redis 直连） | PASS 40/40（2026-11-03） |
| @AUTH-S3 | [local-dev/auth-lockout-check.mjs](../../local-dev/auth-lockout-check.mjs)（改造后 8 断言：预清理残留计数、5 错→403、Redis 直连清 key→恢复 200，替换原 docker exec 依赖） | PASS 8/8（2026-11-03） |

## 回归命令（后端运行于 :5000，E2E_REDIS 指向实例 Redis）

```bash
node local-dev/user-management-e2e.mjs        # 登录/注册/刷新正负向 37 断言（含 @AUTH-S24 刷新令牌携带 typ=normal）
node local-dev/auth-email-code-e2e.mjs        # 注册/找回密码邮箱验证码 39 断言（@AUTH-S26~S35，内置 SMTP 桩）
E2E_REDIS=192.168.50.199:6379 node local-dev/auth-2fa-e2e.mjs      # 两步验证+一体化邮箱码 67 断言（@AUTH-S36~S46）
E2E_REDIS=192.168.50.199:6379 node local-dev/auth-captcha-e2e.mjs  # 图形验证码 40 断言（@AUTH-S48~S53）
E2E_REDIS=192.168.50.199:6379 node local-dev/auth-lockout-check.mjs http://127.0.0.1:5000  # 锁定与恢复 8 断言
dotnet build src/MoAI/MoAI.csproj             # 0 错误
cd ui && npm run typecheck && npm run lint && npm run test
```

## 覆盖率说明

- 自动化 12 个场景（e2e）+ 手动/走查 11 个；@AUTH-S14 为缺陷确认项（预期 400 实际 200），修复后须复跑并改标签。
- 已知未覆盖：refresh_token 真实 7 天过期路径（@AUTH-S11 仅走查）；多实例 rsa_private.key 一致性（见 SOP 排障）。
