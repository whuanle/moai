# feishu 飞书通知模块 验证映射（TDD）

> 关联：[SDD](./sdd.md) ｜ [BDD](./bdd.md) ｜ [TDD](./tdd.md) ｜ [SOP](./sop.md)
> 回归命令：后端运行中执行 `node local-dev/feishu-e2e.mjs`（存量库需先执行 [asserts/feishu_app.sql](../../asserts/feishu_app.sql)）。

| 场景 | 验证物 | 结果（日期） |
|---|---|---|
| @FS-S1~S5 | local-dev/feishu-e2e.mjs（FS-01~05） | PASS 32/32（2026-10-08） |
| @FS-S6~S11 | local-dev/feishu-e2e.mjs（FS-06a~f、07~11；创建即绑定校验、端点下线 405） | PASS 32/32（2026-10-08） |
| @FS-S12 | local-dev/feishu-e2e.mjs（FS-12a~c；FS-12d~g 编辑换 AppID 见 2026-10-26 证据摘要） | PASS 42/42（2026-10-26） |
| @FS-S13~S14 | local-dev/feishu-e2e.mjs（FS-13~14；删除即彻底移除、渠道可重建接入） | PASS 32/32（2026-10-08） |
| @FS-S29~S30 | local-dev/feishu-e2e.mjs（FS-15a~b、16a~d；应用列表回显外部渠道与 applink 地址、禁用隐藏/启用恢复） | PASS 38/38（2026-10-26） |
| @FS-S15 | 代码走查 + 启动日志（假凭证连接终态退出不阻断宿主，见 sop.md 第 4 节） | PASS（2026-09-14） |
| @FS-S16~S18 | @manual（需真实飞书应用，走查 FeishuEventForwarder） | PASS 走查（2026-09-14） |
| @FS-S19~S23 | @manual（需真实飞书应用 + 已配置模型的应用，验收步骤见 sop.md 第 3 节） | 待真实环境验收 |
| @FS-S24~S25、S27~S28 | ui/src/pages/teams/apps/__tests__/AppChannelsSection.test.tsx、__tests__/AppWorkspace.test.tsx（`cd ui && npm run test`） | PASS 498/498（2026-10-08；@FS-S26 已随绑定/解绑下线退役） |
| @FS-S32~S36 | @manual（渠道用户识别：需真实飞书应用 + 已绑定第三方登录的用户，改动与识别规则见 2026-10-28 证据摘要） | 待验收 |

## 证据摘要（2026-09-14）

- 构建：`dotnet build src/MoAI/MoAI.csproj` → 0 error（含 AI.Core 的 AppFeishuMessageHandler）。
- 存量库：`asserts/feishu_app.sql` 已应用于开发库（feishu_app / feishu_app_binding 两表 + 5 索引建齐）。
- E2E：`node local-dev/feishu-e2e.mjs` → 28 pass / 0 fail（鉴权、校验、CRUD、绑定互斥、非法渠道类型 400、级联解绑）。
- OpenAPI：`/openapi/v1.json` 出现全部 5 个 feishu_app 路由（供 syncapi）。
- 长联通路：假凭证连接真实请求飞书 endpoint 收到 `app_id is invalid`，SDK 终态退出、仅记录日志，无未处理异常。
- 回复链路走查：AppFeishuMessageHandler 复用 AppAgentFactory/热态快照/AppChatFlushService（与 AG-UI 同管线），chat↔session 映射 `feishu:chat:*` 30 天滑动；发送走 FeishuApiClient（tenant_access_token 缓存）。

## 证据摘要（2026-09-19，外部渠道页）

- 前端：`npm run typecheck` / `npm run lint` 0 error（8 个 warning 均为存量文件）；`npm run test` → 361/361 PASS（含 AppChannelsSection 9 用例与 AppWorkspace 菜单可见性断言）。
- 后端：`dotnet build src/MoAI/MoAI.csproj` → 0 error 0 warning（本轮后端零改动，Kiota 客户端存量已含 feishu_app 全部路由，无需重新 syncapi）。

## 证据摘要（2026-10-08，外部渠道简化：创建即绑定，下线绑定/解绑）

- 后端：`CreateFeishuAppCommand` 增加可选 `channelType/channelId`（创建即绑定、同一事务落库）；`FeishuAppController` 下线 `POST /{id}/bind`、`POST /{id}/unbind` HTTP 端点（命令保留给知识库外部源内部 MediatR 复用）。`dotnet build src/MoAI/MoAI.csproj` → 0 error。
- 前端：`AppChannelsSection` 仅保留接入（单次创建调用）、停用/启用、删除；删除「绑定已有连接」弹窗与「解绑」操作；i18n 同步清理 zh-CN/en-US。`npm run typecheck`/`npm run lint` 0 error（9 个 warning 均为存量文件）；`npm run test` → 498/498 PASS（AppChannelsSection 7 用例）。
- syncapi：独立构建后端（5002 端口）重新生成 Kiota 客户端，`feishu_app` 的 bind/unbind 路径移除、create 请求体新增 `channelType/channelId`。
- E2E：`node local-dev/feishu-e2e.mjs`（对 5002 新构建）→ 32 pass / 0 fail（创建即绑定校验 404/403/400、同 AppID 互斥 409、bind/unbind 端点 405、删除即彻底移除后同 AppID 重建接入成功）。

## 证据摘要（2026-10-26，应用列表外部渠道入口）

- 后端：`MoAI.App.Core` 新增 `AppExternalChannelHelper`——从 `feishu_app_binding` 应用渠道反查启用的飞书连接（`ChannelId` 落库为原始字符串，内存解析 Guid 比对避免大小写漏配；禁用连接不回显），applink 域名由连接域名推导（`://open.` → `://applink.`，未知域名回退 `applink.feishu.cn`）；`AppItem` 新增 `ExternalChannels`（channelType 枚举 + chatUrl），团队列表 `GET /app/list` 与公开列表 `GET /app/public/list` 映射带出。`dotnet build src/MoAI/MoAI.csproj` → 0 error。
- E2E：`node local-dev/feishu-e2e.mjs` → 38 pass / 0 fail（新增 FS-15a~b 回显 applink 地址、FS-16a~d 禁用隐藏/启用恢复）。
- 前端：`AppPlaza` 与 `TeamApps` 卡片右上角按 `externalChannels` 渲染 `ExternalChannelEntry`（内联官方 logo 的 `FeishuIcon`，Tooltip + 新窗口打开 chatUrl）；i18n `appManage.openInFeishu`（zh/en）。typecheck/lint 0 error（既有 warning 与本次无关），TeamApps/AppPlaza/AppConfigSection 相关测试全绿；浏览器走查两类卡片图标与点击地址正确（applink 弹窗被内嵌测试浏览器拦截，真实浏览器唤起飞书）。

## 证据摘要（2026-10-26，编辑连接：可修改飞书 AppID）

- 后端：`UpdateFeishuAppCommand` 增加可选 `appId`（留空保持不变），Handler 全局唯一校验（排除自身）后赋值并触发重连；`dotnet build src/MoAI/MoAI.csproj -o .builds/dingtalk` → 0 error。
- 前端：`AppChannelsSection` 操作列新增「编辑」（编辑弹窗复用创建表单，AppSecret 留空=保持不变、必填规则仅创建态生效），启用/停用不带 AppID 避免并发覆盖；i18n zh/en +4 键；`AppChannelsSection.test.tsx` 增编辑 2 用例。
- E2E：`node local-dev/feishu-e2e.mjs`（对 5002 新构建）→ 42 pass / 0 fail（FS-12d 换 AppID、12e 回显、12f 占用 409、12g 留空保持）。

| @FS-S31 | 双实例实测（与钉钉共用 RedisAppLease 同轮验证）：飞书应用分散到两实例（公平份额释放 + 争抢）、杀一实例后另一实例约 40s 接管其应用 | PASS（2026-10-26） |

## 证据摘要（2026-10-27，外部渠道页布局收敛：接入指引默认收缩 + 按钮上移卡片标题）

- 用户反馈「接入指引整段 Alert 把页面撑开」。`AppChannelsSection`：接入指引由卡片上方独立 Alert 改为卡片内 `Collapse`（ghost，默认收缩、点击展开）；「接入飞书应用」「刷新」按钮上移至卡片标题右侧（`Card extra`），移除 DataTable toolbar 独立一行；未发布警告保留卡片上方。
- 验证：`npm run typecheck` 0 error；`npm run lint` 0 error；`AppChannelsSection.test.tsx` 9/9（三渠道分区合计 25/25）。
- 迭代（同日，用户反馈指引展开后堆成一段）：指引文案按「N. 」序号拆分为有序列表逐条展示——新增共享组件 `AppChannelGuide`（三渠道分区共用）与 `utils/text.ts` `splitNumberedSteps`（zh/en 通用，去序号与段尾分号）；AppChannelsSection.test 增「默认收缩、展开逐条分行」断言。

## 证据摘要（2026-10-28，渠道用户识别与会话归属 + 渠道放开流程应用）

- 后端（三渠道 Handler 同构，飞书侧）：`FeishuReceivedMessage` 增解析 `sender.sender_id`（open_id/union_id）；`ChannelUserMatchService`（AI.Core 新增）按 open_id+union_id 双键反查 `user_oauth_connection`（命中唯一且用户可用才归属，歧义/停用/未命中降级匿名）；归属会话 `CreateUserId=用户id、UserType=Normal`、Redis 键带 `:u{userId}` 后缀（群聊中各绑定成员独立会话）；应用门禁放宽为 Agent/Workflow 双类型。`dotnet build src/MoAI/MoAI.csproj -o .builds/cum` → 0 error。
- 配套：`user_oauth_connection` 增 `union_id` 列（幂等 DDL，启动自动补列，见 [../oauthconnect/tdd.md](../oauthconnect/tdd.md)）；飞书第三方登录绑定时同时记录 union_id。
- 验收：@FS-S32~S36 需真实飞书应用与已绑定用户，留待人工验收（识别规则与降级矩阵见 [sdd](./sdd.md) 关键决策 7、[bdd](./bdd.md#fs-s32)）。
