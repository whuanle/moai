# 系统设置（Settings）验证映射（TDD）

> 关联：[SDD](./sdd.md) ｜ [BDD](./bdd.md)（场景定义） ｜ [TDD](./tdd.md) ｜ [SOP](./sop.md)
> 本文只做「场景 → 验证物 → 结果」映射；场景语义见 BDD 编号，设计依据见 SDD。

## 场景映射表

| 场景 | 验证物 | 结果（日期） |
|---|---|---|
| @SET-S1 | [local-dev/audit-345.mjs](../../local-dev/audit-345.mjs)（root/admin 查询，items 含 OPEN_NEO4J；初验为 SOP curl 脚本） | PASS（2026-09-01，2026-09-10 改 key 复跑） |
| @SET-S2 | 同上（member 查询 403「只有管理员可以访问设置项」） | PASS（2026-09-01） |
| @SET-S3 | 同上（未登录 401） | PASS（2026-09-01） |
| @SET-S4 | 同上（root 保存 OPEN_NEO4J=true → 回读 "true"） | PASS（2026-09-01，2026-09-10 改 key 复跑） |
| @SET-S5 | @manual 代码走查 + 浏览器走查（保存 false 回读，对称分支） | PASS（2026-09-01） |
| @SET-S6、@SET-S7 | [local-dev/audit-345.mjs](../../local-dev/audit-345.mjs)（root key 与 not_exist_key 均 400「无效的配置项.」） | PASS（2026-09-01） |
| @SET-S8 | @manual 代码走查（`SettingsService.SaveSettingAsync` 无记录插入分支） | PASS（2026-09-01） |
| @SET-S9 | @manual 代码走查（Controller `!IsRoot` 分支；member 403 见 @SET-S2 同轮实证） | PASS（2026-09-01） |
| @SET-S11 ~ @SET-S15 | @manual 浏览器走查（[SOP 第 4 节](./sop.md)；三轮浏览器全页面回归含设置页） | PASS（2026-09-01/09-02） |
| @SET-S16 ~ @SET-S18 | `ui/src/pages/settings/__tests__/Settings.test.tsx`（root 开启并保存三项连接、关闭仅存 OPEN_NEO4J、非 root 被重定向，4/4） | PASS（2026-09-10） |
| @SET-S19 | @manual 代码走查（`KnowledgeGraphSettingsService.GetAsync`：未开启短路返回 `Enabled=false`；开启回传连接项，缺失走 `SettingDefinitions` 默认值） | PASS（2026-09-10） |

## 回归命令（后端运行于 :5210）

```bash
node local-dev/audit-345.mjs                 # 含 settings 门禁/保存/回读分支
cd ui && npm run typecheck && npm run lint && npm run test
npx vitest run src/pages/settings/__tests__/Settings.test.tsx   # 知识图谱开关/连接项保存
```

## 覆盖率说明

- 共 18 个场景：自动化 9 个（@SET-S1~S4、S6~S7、S16~S18），走查/手动 9 个（@SET-S5、S8~S9、S11~S15、S19）；对称分支（关闭开关、admin 保存 403、首写建行、知识图谱读取未开启短路）以代码走查覆盖，补自动化成本低。
- 门禁为 Controller 层实现，若重构须复跑 @SET-S2/@SET-S6/@SET-S9。
- 知识图谱前端场景 @SET-S16~S18 由 `Settings.test.tsx` 覆盖；@SET-S19 为读取服务代码走查，接入知识库后应补集成测试。
- 已移除 @SET-S10（oauth_auto_register 第三方自动注册直通）：该设置项无任何业务代码消费，随设置项一并删除，编号作废不复用。
