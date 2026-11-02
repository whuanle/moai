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
| @SET-S20、@SET-S21 | [local-dev/sandbox-limits-e2e.mjs](../../local-dev/sandbox-limits-e2e.mjs)（root 保存三项上限并回读、TTL 越界与 CPU/内存格式非法 400） | PASS 21/21（2026-09-19） |
| @SET-S22 | [tests/MoAI.App.Tests](../../tests/MoAI.App.Tests/)（`SandboxQuantityTests` K8s 数量解析）+ @manual 代码走查（`SandboxSettingsService.GetLimitsAsync` 回退默认） | PASS 35/35（2026-09-19） |
| @SET-S23 | `ui/src/pages/settings/__tests__/Settings.test.tsx`（沙箱卡片折叠回显、保存三项、非法格式拦截不发请求） | PASS 8/8（2026-09-19） |
| @SET-S24 ~ @SET-S29 | [local-dev/settings-logo-e2e.mjs](../../local-dev/settings-logo-e2e.mjs)（匿名 serverinfo 暴露 logoPath、root 上传/生效、member 403 与未登录 401、伪造 objectKey 404、空标识恢复默认） | PASS 11/11（2026-09-20；2026-10-29 起脚本开头快照三项原值、场景内清空断言后恢复，不再破坏共享实例站点配置） |
| @SET-S30 | `ui/src/pages/settings/__tests__/Settings.test.tsx`（网站信息卡片 Logo 分区回显/上传刷新/非图片拦截/确认恢复默认） | PASS 12/12（2026-09-20，2026-10-29 卡片合并后复核） |
| @SET-S31 ~ @SET-S33 | [local-dev/settings-logo-e2e.mjs](../../local-dev/settings-logo-e2e.mjs)（网站名称：root 保存后 serverinfo/设置项生效、超长 400、member 403、清空回退配置默认） | PASS 7/7（2026-09-20） |
| @SET-S34 | `ui/src/pages/settings/__tests__/Settings.test.tsx`（网站信息卡片网站名称分区回显、去空白提交并刷新 serverInfo、清空提交空串） | PASS 2/2（2026-09-20，2026-10-29 卡片合并后复核） |
| @SET-S35 ~ @SET-S40 | [local-dev/settings-logo-e2e.mjs](../../local-dev/settings-logo-e2e.mjs)（登录页背景图：匿名 serverinfo 暴露 loginBackgroundPath、root 上传/生效、member 403 与未登录 401、伪造 objectKey 404、空标识恢复默认） | PASS 10/10（2026-10-28） |
| @SET-S41 | `ui/src/pages/settings/__tests__/Settings.test.tsx`（网站信息卡片背景分区回显/占位、上传刷新、非图片拦截、确认恢复默认） | PASS 4/4（2026-10-28，文件合计 18/18；2026-10-29 卡片合并后文件合计 23/23） |
| @SET-S42 | @manual 浏览器走查（root 上传横图后登录页整页铺满；清空后回退默认渐变） | PASS（2026-10-28） |
| @SET-S43 | `ui/src/pages/auth/__tests__/{Login,Register}.test.tsx`（登录/注册卡片主标题展示自定义名称并同步 document.title、空值回退默认 MoAI） | PASS 4/4（2026-10-28） |
| @SET-S44、@SET-S45 | [local-dev/wiki-e2e.mjs](../../local-dev/wiki-e2e.mjs)（WK-12a…c 网页端门禁与恢复）+ [local-dev/wiki-external-e2e.mjs](../../local-dev/wiki-external-e2e.mjs)（WX-11a/c/f 外部接口同门禁、WX-11b 无点号/重复/空串 400） | PASS WK 35/35 + WX 52/52（2026-10-29，5000 实例） |
| @SET-S46 | [local-dev/wiki-external-e2e.mjs](../../local-dev/wiki-external-e2e.mjs)（WX-11d/e：放开 .sh 上传 → complete 自动提取 → content 为原文本）；二进制拒绝为 @manual 代码走查（`WikiTextFileReader` NUL 嗅探） | PASS（2026-10-29） |
| @SET-S47 | `ui/src/pages/settings/__tests__/Settings.test.tsx`（文件类型标签回显、逗号归一提交、非法扩展名拦截不发请求；卡片移至知识图谱上方） | PASS 20/20（2026-10-29） |
| @SET-S61 ~ @SET-S63 | [local-dev/settings-smtp-e2e.mjs](../../local-dev/settings-smtp-e2e.mjs)（SET-61/62/63/64：渠道连接配置在通知中心校验 400、绑定保存回读与非法绑定值 400（含 SET-63g~j 验证码通知模板绑定 id 校验）、member 403 与未登录 401） | PASS 36/36（2026-11-02） |
| @SET-S64 | [local-dev/settings-smtp-e2e.mjs](../../local-dev/settings-smtp-e2e.mjs)（SET-65a~g：serverinfo passwordResetEnabled 随开关/绑定/渠道可用性联动） | 同上 |
| @SET-S65、@SET-S66 | [local-dev/settings-smtp-e2e.mjs](../../local-dev/settings-smtp-e2e.mjs)（SET-66/67：渠道自带配置对本地 SMTP 桩全链路发信，信封与 DATA 头；AUTH PLAIN 凭据核对） | 同上 |
| @SET-S67 | `ui/src/pages/settings/__tests__/Settings.test.tsx`（邮箱验证卡片折叠回显、未启用无绑定下拉、绑定下拉来源与停用标记、验证码模板绑定下拉回显内置标识/清空提交空绑定、保存仅提交开关+绑定+模板 id；文件合计 27/27） | PASS（2026-11-02） |
| @SET-S56、@SET-S69 | [local-dev/auth-email-code-e2e.mjs](../../local-dev/auth-email-code-e2e.mjs)（SET-56a 开关非法值 400、SET-69a/b 模板 id 非法 400、SET-69c 保存去空白回读；模板渲染语义链接到 [@AUTH-S31](../auth/bdd.md#auth-s31)） | PASS（2026-11-02，脚本合计 39/39） |
| @SET-S68 | @manual 代码走查（`EmailSettingsService.GetAsync` 开关+渠道可用性联动、`SystemEmailSenderService` 按绑定渠道经 `SmtpMailTransport` 发送） | PASS（2026-11-02） |

## 回归命令（后端运行于 :5210）

```bash
node local-dev/audit-345.mjs                 # 含 settings 门禁/保存/回读分支
node local-dev/sandbox-limits-e2e.mjs        # 沙箱上限设置读写与格式校验
node local-dev/settings-logo-e2e.mjs         # 网站 Logo 上传/恢复与门禁 + 网站名称保存/回退/校验 + 登录页背景图上传/恢复/门禁
node local-dev/settings-smtp-e2e.mjs          # 邮箱验证与 SMTP 服务绑定：渠道连接校验/绑定回读/门禁/serverinfo 联动/渠道测试发信与认证（本地 SMTP 桩，零真实依赖）
node local-dev/auth-email-code-e2e.mjs       # 注册/找回密码邮箱验证码全链路 + 验证码通知模板绑定校验（@SET-S56/S69，内置 SMTP 桩）
node local-dev/wiki-e2e.mjs                  # 知识库上传上限/允许文件类型（WK-11/WK-12）
node local-dev/wiki-external-e2e.mjs         # 外部接口 + 允许文件类型/纯文本兜底（WX-11）
cd ui && npm run typecheck && npm run lint && npm run test
npx vitest run src/pages/settings/__tests__/Settings.test.tsx   # 知识图谱开关/连接项保存 + 沙箱上限卡片 + 网站信息卡片（网站名称/Logo/登录页背景三分区） + 知识库文件类型卡片 + 邮箱验证绑定卡片
```

## 覆盖率说明

- 共 60 个场景（@SET-S48~S55 随 SMTP 直连配置下线作废不复用）：自动化 46 个（@SET-S1~S4、S6~S7、S16~S18、S20~S47、S56~S58、S61~S67），走查/手动 14 个（@SET-S5、S8~S9、S11~S15、S19、S42、S68）；对称分支（关闭开关、admin 保存 403、首写建行、知识图谱读取未开启短路）以代码走查覆盖，补自动化成本低。
- `settings-smtp-e2e.mjs` 会真实写入 SMTP_ENABLED/SMTP_CHANNEL_ID 并创建临时 SMTP 渠道：脚本开头快照原值、结尾 SET-90/SET-91 恢复与删渠道，共享开发实例上执行不破坏现场。
- `settings-logo-e2e.mjs` 会真实写入 SYSTEM_NAME/SYSTEM_LOGO/LOGIN_BACKGROUND：2026-10-29 起脚本开头快照三项原值、清空类场景（SET-29/33/40）断言完成后即恢复，结尾 SET-90 校验恢复结果——在共享开发实例上执行不再清掉已配置的站点品牌（此前一次回归把用户已设置的名称/Logo/背景清空，靠探活快照手工恢复）。
- 门禁为 Controller 层实现，若重构须复跑 @SET-S2/@SET-S6/@SET-S9。
- 知识图谱前端场景 @SET-S16~S18 由 `Settings.test.tsx` 覆盖；@SET-S19 为读取服务代码走查，接入知识库后应补集成测试。
- 已移除 @SET-S10（oauth_auto_register 第三方自动注册直通）：该设置项无任何业务代码消费，随设置项一并删除，编号作废不复用。
