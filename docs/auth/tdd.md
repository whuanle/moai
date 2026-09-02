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
| @AUTH-S4、@AUTH-S11、@AUTH-S12、@AUTH-S18 | @manual 代码走查（Login/RefreshToken Handler 分支；refresh TTL>7 天与 oauth:bind 过期路径） | PASS（2026-09-01） |
| @AUTH-S13、@AUTH-S15 ~ @AUTH-S17、@AUTH-S19 | OAuth 全链路 12/12（本地 mock OIDC Provider，存档 [../user-management/sop.md](../user-management/sop.md) 第三轮） | PASS 12/12（2026-09-02） |
| @AUTH-S14 | `curl "http://127.0.0.1:5210/api/auth/oauth_prividers?redirectUrl=http://evil.com"` | **200（非 400）**——复核确认死代码缺陷（2026-09-01） |
| @AUTH-S20 ~ @AUTH-S23 | @manual 浏览器走查（[SOP 第 4 节](./sop.md)；轮 11 独立验证 auth 前端链路） | PASS（2026-09-01/09-02） |

## 回归命令（后端运行于 :5210）

```bash
node local-dev/user-management-e2e.mjs        # 登录/注册/刷新正负向 34 断言
node local-dev/auth-lockout-check.mjs         # 锁定与恢复（脚本内置 RSA 加密）
dotnet build src/MoAI/MoAI.csproj             # 0 错误
cd ui && npm run typecheck && npm run lint && npm run test
```

## 覆盖率说明

- 自动化 12 个场景（e2e）+ 手动/走查 11 个；@AUTH-S14 为缺陷确认项（预期 400 实际 200），修复后须复跑并改标签。
- 已知未覆盖：refresh_token 真实 7 天过期路径（@AUTH-S11 仅走查）；多实例 rsa_private.key 一致性（见 SOP 排障）。
