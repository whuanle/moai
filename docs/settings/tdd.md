# 系统设置（Settings）验证映射（TDD）

> 关联：[SDD](./sdd.md) ｜ [BDD](./bdd.md)（场景定义） ｜ [TDD](./tdd.md) ｜ [SOP](./sop.md)
> 本文只做「场景 → 验证物 → 结果」映射；场景语义见 BDD 编号，设计依据见 SDD。

## 场景映射表

| 场景 | 验证物 | 结果（日期） |
|---|---|---|
| @SET-S1 | [local-dev/audit-345.mjs](../../local-dev/audit-345.mjs)（root/admin 查询，items 含 oauth_auto_register；初验为 SOP curl 脚本） | PASS（2026-09-01） |
| @SET-S2 | 同上（member 查询 403「只有管理员可以访问设置项」） | PASS（2026-09-01） |
| @SET-S3 | 同上（未登录 401） | PASS（2026-09-01） |
| @SET-S4 | 同上（root 保存 true → 回读 "true"） | PASS（2026-09-01） |
| @SET-S5 | @manual 代码走查 + 浏览器走查（保存 false 回读，对称分支） | PASS（2026-09-01） |
| @SET-S6、@SET-S7 | [local-dev/audit-345.mjs](../../local-dev/audit-345.mjs)（root key 与 not_exist_key 均 400「无效的配置项.」） | PASS（2026-09-01） |
| @SET-S8 | @manual 代码走查（`SettingsService.SaveSettingAsync` 无记录插入分支） | PASS（2026-09-01） |
| @SET-S9 | @manual 代码走查（Controller `!IsRoot` 分支；member 403 见 @SET-S2 同轮实证） | PASS（2026-09-01） |
| @SET-S10 | OAuth 全链路 12/12 之「直通」分支（mock OIDC，存档 [../user-management/sop.md](../user-management/sop.md) 第三轮） | PASS 12/12（2026-09-02） |
| @SET-S11 ~ @SET-S15 | @manual 浏览器走查（[SOP 第 4 节](./sop.md)；三轮浏览器全页面回归含设置页） | PASS（2026-09-01/09-02） |

## 回归命令（后端运行于 :5210）

```bash
node local-dev/audit-345.mjs                 # 含 settings 门禁/保存/回读分支
cd ui && npm run typecheck && npm run lint && npm run test
```

## 覆盖率说明

- 自动化 7 个场景 + 走查/手动 8 个；对称分支（关闭开关、admin 保存 403、首写建行）以代码走查覆盖，补自动化成本低。
- 门禁为 Controller 层实现，若重构须复跑 @SET-S2/@SET-S6/@SET-S9。
