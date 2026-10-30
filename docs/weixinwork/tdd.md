# weixinwork 企业微信渠道模块 验证映射（TDD）

> 关联：[SDD](./sdd.md) ｜ [BDD](./bdd.md) ｜ [TDD](./tdd.md) ｜ [SOP](./sop.md)
> 回归命令：后端运行中执行 `node local-dev/weixinwork-e2e.mjs`（存量库需先执行 [asserts/weixinwork_app.sql](../../asserts/weixinwork_app.sql)）。
> E2E 内置本地 qyapi 桩（gettoken/message/send/appchat/send，经连接的 `apiEndpoint` 注入）+ 回调加解密模拟 + OpenAI 兼容桩模型，全链路零真实依赖、零 SKIP。

| 场景 | 验证物 | 结果（日期） |
|---|---|---|
| @WX-S1~S6 | local-dev/weixinwork-e2e.mjs（WX-01~05、11） | PASS 54/54（2026-10-27） |
| @WX-S9~S13 | local-dev/weixinwork-e2e.mjs（WX-06a~e、07~10；创建即绑定校验） | PASS 54/54（2026-10-27） |
| @WX-S7 | local-dev/weixinwork-e2e.mjs（WX-12a~g；禁用/留空保持/换凭证/占用 409/Member 403） | PASS 54/54（2026-10-27） |
| @WX-S8 | local-dev/weixinwork-e2e.mjs（WX-13~14；删除即彻底移除、渠道可重建接入） | PASS 54/54（2026-10-27） |
| @WX-S14~S16 | local-dev/weixinwork-e2e.mjs（WX-15a~c；echostr 回显/签名 400/连接 404） | PASS 54/54（2026-10-27） |
| @WX-S17 | local-dev/weixinwork-e2e.mjs（WX-16a~c；受理 success → 桩模型 → message/send 送达） | PASS 54/54（2026-10-27） |
| @WX-S18 | local-dev/weixinwork-e2e.mjs（WX-17a~b；ChatId → appchat/send 送达） | PASS 54/54（2026-10-27） |
| @WX-S19 | local-dev/weixinwork-e2e.mjs（WX-18；同 MsgId 重发只回复一次） | PASS 54/54（2026-10-27） |
| @WX-S20 | local-dev/weixinwork-e2e.mjs（WX-19；同成员第二条消息仍回复） | PASS 54/54（2026-10-27） |
| @WX-S21 | local-dev/weixinwork-e2e.mjs（WX-20；image 推送受理不回复） | PASS 54/54（2026-10-27） |
| @WX-S22~S24 | local-dev/weixinwork-e2e.mjs（WX-21a~c；Token 签名 400/receiveid 400/连接 404） | PASS 54/54（2026-10-27） |
| @WX-S25~S26 | local-dev/weixinwork-e2e.mjs（WX-22a~b；禁用连接/未发布应用不回复） | PASS 54/54（2026-10-27） |
| @WX-S27~S30 | ui/src/pages/teams/apps/__tests__/AppWeixinWorkChannelsSection.test.tsx（8 用例：过滤/未发布提示/接入/编辑回显+回调地址/密钥留空保持/停用确认/删除确认/只读；编号 2026-10-28 顺延拆分，原四场景曾重号 @WX-S27） | PASS 8/8（2026-10-27） |
| @WX-S31~S34 | @manual（渠道用户识别：需真实企业微信应用 + 已绑定第三方登录的用户，改动与识别规则见 2026-10-28 证据摘要） | 待验收 |
| 真实企业微信验收 | @manual（需企业自建应用与公网地址，验收步骤见 sop.md 第 3 节） | 待真实环境验收 |

## 证据摘要（2026-10-27）

- 构建：`dotnet build src/MoAI/MoAI.csproj -o .builds/wecom-verify` → 0 error（宿主 5000 实例运行中锁 bin 输出目录，按仓库惯例独立输出构建）；weixinwork 新文件 0 warning。
- 数据库：`asserts/weixinwork_app.sql` 已应用于开发库 moai_v2（weixinwork_app / weixinwork_app_binding 两表 + 5 索引建齐，Npgsql 直连执行）。
- OpenAPI：重启后端后 `/openapi/v1.json` 出现全部 4 个 weixinwork_app 路由（list/POST/PUT·DELETE/callback）；`cd ui && npm run syncapi` 重新生成 Kiota 客户端。
- 后端 E2E：`node local-dev/weixinwork-e2e.mjs` → **54 pass / 0 fail / 0 skip**（门禁、校验、CRUD、创建即绑定、CorpId+AgentId 互斥 409、非成员 404、级联解绑、删除后重建接入、URL 验证回显、推送验签/解密/receiveid 防护、单聊 message/send 与群聊 appchat/send 全链路（桩模型回复回显）、MsgId 去重、会话延续、非文本忽略、禁用/未发布不回复）。
- 前端：`npm run typecheck` 0 error；`npm run lint` 0 error（10 个 warning 均为存量文件）；vitest 分区测试 8/8（全量 `npm run test` 在本机并行负载下有与本次改动无关的既有抖动，失败文件单跑全绿）。
- i18n：`dotnet run --project tool/I18nScanner` 重新扫描，企业微信 BusinessException 新增 9 键（zh-CN/en-US 已翻译）；UI 文案 `appWeixinWorkChannels`（zh/en 同步）。
- 解决方案：`dotnet sln add --solution-folder weixinwork` 登记三项目；`MoAI.csproj`/`MainModule` 挂载 `WeixinWorkCoreModule`。

## 证据摘要（2026-10-27，外部渠道页布局收敛：接入指引默认收缩 + 按钮上移卡片标题）

- 同飞书/钉钉分区布局调整（用户反馈「接入指引整段 Alert 把页面撑开」）：`AppWeixinWorkChannelsSection` 接入指引改为卡片内 `Collapse` 默认收缩、点击展开；「接入企业微信应用」「刷新」上移卡片标题右侧（`Card extra`），移除 DataTable toolbar 行；未发布警告保留卡片上方。bdd @WX-S27 场景措辞同步。
- 验证：`npm run typecheck` 0 error；`npm run lint` 0 error；`AppWeixinWorkChannelsSection.test.tsx` 8/8（三渠道分区合计 25/25）。
- 迭代（同日，用户反馈指引展开后堆成一段）：指引文案按「N. 」序号拆分为有序列表逐条展示——新增共享组件 `AppChannelGuide`（三渠道分区共用）与 `utils/text.ts` `splitNumberedSteps`（zh/en 通用，去序号与段尾分号）；AppChannelsSection.test 增「默认收缩、展开逐条分行」断言。

## 证据摘要（2026-10-28，渠道用户识别与会话归属 + 渠道放开流程应用）

- 后端（企业微信侧）：回调推送 `FromUserName`（成员 userid）作为识别键，`ChannelUserMatchService`（AI.Core 新增）反查 `user_oauth_connection`（命中唯一且用户可用才归属，歧义/停用/未命中降级匿名）；归属会话 `CreateUserId=用户id、UserType=Normal`、Redis 键带 `:u{userId}` 后缀；应用门禁放宽为 Agent/Workflow 双类型。`dotnet build src/MoAI/MoAI.csproj -o .builds/cum` → 0 error。
- 配套：企业微信连接器第三方登录补原生路由（原走 custom OIDC 恒不可用，见 [../oauthconnect/tdd.md](../oauthconnect/tdd.md)）。
- 顺带修正：bdd 应用工作台分区四场景原重号 @WX-S27，顺延拆分为 @WX-S27~S30；文件尾部游离代码围栏移除。
- 验收：@WX-S31~S34 需真实企业微信应用与已绑定用户，留待人工验收（识别规则见 [sdd](./sdd.md) 关键决策 7、[bdd](./bdd.md#wx-s31)）。
