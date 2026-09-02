# 账号自助（Account Self-Service）验证映射（TDD）

> 关联：[SDD](./sdd.md) ｜ [BDD](./bdd.md)（场景定义） ｜ [TDD](./tdd.md) ｜ [SOP](./sop.md)
> 本文只做「场景 → 验证物 → 结果」映射；场景语义见 BDD 编号，设计依据见 SDD。

## 场景映射表

| 场景 | 验证物 | 结果（日期） |
|---|---|---|
| @ACC-S1、@ACC-S2 | [local-dev/audit-345.mjs](../../local-dev/audit-345.mjs)（userinfo 正向 / 未登录 401；初验为 SOP curl 脚本） | PASS（2026-09-01） |
| @ACC-S3 | 深度回归「权限门禁」（存档 [../user-management/sop.md](../user-management/sop.md) 第二轮 68/68） | PASS（2026-09-01） |
| @ACC-S4 | [local-dev/audit-345.mjs](../../local-dev/audit-345.mjs)（改资料→复查新值） | PASS（2026-09-01） |
| @ACC-S5、@ACC-S6 | @manual 代码走查（`UpdateUserInfoCommandHandler` 覆盖语义） | PASS（2026-09-01） |
| @ACC-S7、@ACC-S8、@ACC-S10 | [local-dev/audit-345.mjs](../../local-dev/audit-345.mjs)（改密成功重登 / 旧密错 400 / 弱密码 400） | PASS（2026-09-01） |
| @ACC-S9 | 同上（非合法密文 400「原密码错误」） | PASS（2026-09-01） |
| @ACC-S11 | [local-dev/audit-345.mjs](../../local-dev/audit-345.mjs)（空 objectKey 400） | PASS（2026-09-01） |
| @ACC-S12 | 深度回归（伪造 objectKey 404，修复后复测） | PASS 68/68（2026-09-01） |
| @ACC-S13 | @manual 浏览器走查（依赖 storage 预上传直传，见 [SOP 第 3 节](./sop.md)） | PASS（2026-09-01） |
| @ACC-S14 | OAuth 全链路 12/12（mock OIDC，存档 [../user-management/sop.md](../user-management/sop.md) 第三轮） | PASS 12/12（2026-09-02） |
| @ACC-S15 ~ @ACC-S18 | @manual 代码走查（绑定冲突分支：幂等/他人绑定/换绑/第三方故障 500） | PASS（2026-09-01） |
| @ACC-S19 | @manual（依赖真实第三方授权链路，mock 环境走查） | PASS（2026-09-02） |
| @ACC-S20 | [local-dev/audit-345.mjs](../../local-dev/audit-345.mjs)（过期临时标识 403） | PASS（2026-09-01） |
| @ACC-S21、@ACC-S23 | [local-dev/audit-345.mjs](../../local-dev/audit-345.mjs)（绑定列表 / 解绑未绑定 404） | PASS（2026-09-01） |
| @ACC-S22 | OAuth 全链路 12/12（解绑分支，存档同上） | PASS 12/12（2026-09-02） |
| @ACC-S24 ~ @ACC-S28 | @manual 浏览器走查（[SOP 第 4 节](./sop.md)；三轮浏览器全页面回归含账号设置页） | PASS（2026-09-01/09-02） |

## 回归命令（后端运行于 :5210）

```bash
node local-dev/audit-345.mjs                 # account/settings 后端分支（初验命令见 SOP 第 4 节存档）
cd ui && npm run typecheck && npm run lint && npm run test
```

## 覆盖率说明

- 自动化 13 个场景 + 走查/手动 15 个；绑定冲突四分支（@ACC-S15 ~ @ACC-S18）仅代码走查，补自动化需 mock 第三方 Provider。
- 修复史均经深度回归（68/68）复核：禁用拦截、超长昵称 400、伪造 objectKey 404（见 [SDD 已知问题](./sdd.md)）。
