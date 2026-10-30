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

## 回归命令（后端运行于 :5210）

```bash
node local-dev/user-management-e2e.mjs        # 登录/注册/刷新正负向 37 断言（含 @AUTH-S24 刷新令牌携带 typ=normal）
node local-dev/auth-email-code-e2e.mjs        # 注册/找回密码邮箱验证码 34 断言（@AUTH-S26~S35，内置 SMTP 桩）
node local-dev/auth-lockout-check.mjs         # 锁定与恢复（脚本内置 RSA 加密）
dotnet build src/MoAI/MoAI.csproj             # 0 错误
cd ui && npm run typecheck && npm run lint && npm run test
```

## 覆盖率说明

- 自动化 12 个场景（e2e）+ 手动/走查 11 个；@AUTH-S14 为缺陷确认项（预期 400 实际 200），修复后须复跑并改标签。
- 已知未覆盖：refresh_token 真实 7 天过期路径（@AUTH-S11 仅走查）；多实例 rsa_private.key 一致性（见 SOP 排障）。
