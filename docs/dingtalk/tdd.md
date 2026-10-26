# dingtalk 钉钉渠道模块 验证映射（TDD）

> 关联：[SDD](./sdd.md) ｜ [BDD](./bdd.md) ｜ [TDD](./tdd.md) ｜ [SOP](./sop.md)
> 回归命令：后端运行中执行 `node local-dev/dingtalk-e2e.mjs`（存量库需先执行 [asserts/dingtalk_app.sql](../../asserts/dingtalk_app.sql)）。

| 场景 | 验证物 | 结果（日期） |
|---|---|---|
| @DT-S1~S6 | local-dev/dingtalk-e2e.mjs（DT-01~05、11） | PASS 37/37（2026-10-26） |
| @DT-S7~S11 | local-dev/dingtalk-e2e.mjs（DT-06a~e、07~10；创建即绑定校验） | PASS 37/37（2026-10-26） |
| @DT-S12 | local-dev/dingtalk-e2e.mjs（DT-12a~h；12e~h 编辑换 AppKey/占用 409/留空保持） | PASS 37/37（2026-10-26） |
| @DT-S13~S14 | local-dev/dingtalk-e2e.mjs（DT-13~14；删除即彻底移除、渠道可重建接入） | PASS 37/37（2026-10-26） |
| @DT-S15 | 启动日志 + 假凭证降级观察（网关 authFailed 仅记错误日志，SDK 内部重试，不阻断宿主，见 sop.md 第 4 节） | PASS（2026-10-26） |
| @DT-S16~S18 | @manual（需真实钉钉应用，走查 DingTalkEventForwarder） | PASS 走查（2026-10-26） |
| @DT-S19~S23 | @manual（需真实钉钉应用 + 已配置模型的应用，验收步骤见 sop.md 第 3 节） | 待真实环境验收 |
| @DT-S24~S26 | ui/src/pages/teams/apps/__tests__/AppDingTalkChannelsSection.test.tsx（`cd ui && npm run test`） | PASS 553/553（2026-10-26，含编辑 2 用例） |

| @DT-S27 | local-dev/dingtalk-e2e.mjs（DT-16a~b；绑定即露出、AppKey 兜底地址）+ MoAI.App.Tests AppExternalChannelHelperTests（钉钉 jumprobot 地址/未回填 AppKey 兜底/双渠道） | PASS 39/39 + 86/86（2026-10-26） |
| @DT-S28 | @manual（需真实钉钉机器人首条消息触发回填；浏览器走查卡片钉钉图标与 jumprobot 地址） | PASS 走查（2026-10-26） |
## 证据摘要（2026-10-26）

- 构建：`dotnet build src/MoAI/MoAI.csproj -o .builds/dingtalk` → 0 error（宿主 5000 实例运行中锁 bin 输出目录，按仓库惯例独立输出构建）。
- 存量库：`asserts/dingtalk_app.sql` 已应用于开发库（dingtalk_app / dingtalk_app_binding 两表 + 5 索引建齐，Npgsql 直连执行）。
- 后端 E2E：`node local-dev/dingtalk-e2e.mjs`（对 5002 新构建）→ 33 pass / 0 fail（鉴权、校验、CRUD、创建即绑定、AppKey 互斥 409、非成员 404、级联解绑、删除后重建接入）。
- 回归：`node local-dev/feishu-e2e.mjs`（同实例）→ 38 pass / 0 fail（同库同进程互不影响）。
- OpenAPI：`/openapi/v1.json` 出现全部 3 个 dingtalk_app 路由；`cd ui && npm run syncapi http://127.0.0.1:5002/openapi/v1.json` 重新生成 Kiota 客户端。
- 长联通路：假凭证创建连接后日志出现「开始启动新的【钉钉Stream客户端】」，网关返回 `authFailed` 仅记一次错误日志，进程无未处理异常。
- 前端：`npm run typecheck` 0 error；`npm run lint` 0 error（10 个 warning 均为存量/并行 WIP 文件）；`npm run test` → 551/551 PASS（77 文件，含 AppDingTalkChannelsSection 6 用例）。
- i18n：`dotnet run --project tool/I18nScanner` 重新扫描，钉钉 BusinessException 新增 6 键（zh-CN/en-US 已翻译）。

## 证据摘要（2026-10-26，编辑连接：可修改钉钉 AppKey）

- 后端：`UpdateDingTalkAppCommand` 增加可选 `appKey`（留空保持不变），Handler 全局唯一校验（排除自身）后赋值并触发重连；`dotnet build src/MoAI/MoAI.csproj -o .builds/dingtalk` → 0 error（同时飞书侧 `UpdateFeishuAppCommand` 加 `appId`，见 docs/feishu/tdd.md）。
- 前端：`AppDingTalkChannelsSection`/`AppChannelsSection` 操作列新增「编辑」（编辑弹窗复用创建表单，AppSecret 留空=保持不变、必填规则仅创建态生效），启用/停用不带 AppKey/AppID 避免并发覆盖；i18n zh/en 各 +4 键；两组件测试各增编辑 2 用例。
- E2E：`node local-dev/dingtalk-e2e.mjs`（对 5002 新构建）→ 37 pass / 0 fail（DT-12e 换 AppKey、12f 回显、12g 占用 409、12h 留空保持）；feishu-e2e 同实例回归 42/42。
- syncapi：重生成 Kiota 客户端（两个 update 请求模型新增 appId/appKey 字段）。
- 前端：typecheck 0 error；vitest 78 文件全过（其中 AppInfoSection 有 1 个非用例失败的 unhandled rejection，属并行 WIP 文件）。

## 证据摘要（2026-10-26，应用列表外部对话入口：钉钉）

- 后端：`dingtalk_app` 增列 `chatbot_user_id`（机器人的 dingtalk id，回调 `chatbotUserId`；实体/EF 配置/asserts 增量 `alter table ... add column if not exists` 已对开发库 moai_v2 执行）；`DingTalkEventForwarder` 收到机器人消息后回填（已一致不写库）；`AppExternalChannelType` 增 `dingtalk`（枚举成员名取 `Dingtalk`——NSwag 按成员名 camelCase 生成 OpenAPI 枚举值）；`AppExternalChannelHelper` 反查钉钉绑定生成 `dingtalk://dingtalkclient/action/jumprobot?dingtalkid=…`（未回填不露出，禁用不露出）。`dotnet build src/MoAI/MoAI.csproj` → 0 error。
- 单测：MoAI.App.Tests **86/86**（新增 `AppExternalChannelHelperTests` 6 例：飞书 applink 域名推导/未知域名回退、钉钉 jumprobot URL 编码、未回填不露出、双渠道并存、禁用与他用渠道忽略）。
- E2E：`node local-dev/dingtalk-e2e.mjs` → **39 pass / 0 fail**（新增 DT-16a~b：绑定未回填不露出入口）。
- 前端：`ExternalChannelEntry` 支持多渠道（飞书/钉钉图标并存，`DingTalkIcon` 内联官方 logo SVG）；i18n `appManage.openInDingTalk`（zh/en）；typecheck/lint 0 error；浏览器走查团队卡片双图标与钉钉点击地址正确（真实唤起需钉钉客户端）。
- 迭代（同日，用户反馈「内部应用少了钉钉图标」）：取消「回填后才露出」门控——绑定启用即露出，未回填时 chatUrl 用 AppKey（robotCode）兜底，首条机器人消息回填后自动切换为 chatbotUserId 精确链接；单测与 DT-16 同步改写（未回填断言 AppKey 兜底地址），实测用户存量应用「开发问题排查」（绑定 dingiylsuwusq2y6iqju、未回填）列表已返回双渠道。
- **再迭代（同日，用户实测「点击图标只打开钉钉、打不开机器人」）**：AppKey 兜底链接被证实为坏体验——jumprobot 只认回调 `chatbotUserId`（加密 dingtalk id），传 AppKey 钉钉只会拉起 App 找不到机器人。回退兜底逻辑恢复「未回填不露出」；单测「未回填用 AppKey 兜底生成链接」改写为「未回填不生成入口」，DT-16a~b 断言反向（不露出）；钉钉渠道接入指引补首条消息激活说明（zh/en）。单测 86/86、dingtalk-e2e 39/39、typecheck 0 error。图标激活路径：重启后端（连接在线）→ 钉钉内搜索机器人名称发任意消息 → 转发器回填 chatbot_user_id → 图标自动出现且指向精确链接。

## 证据摘要（2026-10-26，修复：真实凭证连接成功却显示「离线」）

- **现象**：真实凭证接入后列表恒显示离线（@DT-S15 观察项）。
- **根因**：`DingTalkStreamClient.Start()` 在连接成功建立后**立即返回**（网关换票 → WebSocket 就绪即完成 initialStartCompletion），与飞书 `WssClient.StartAsync`（阻塞到断开）语义相反；Manager 的 RunTask 在 `finally` 里置离线，`OnStarted` 置 true 后立刻被打回 false。连接本体由 SDK 生命周期维持（消息链路实际可用），纯属状态显示错误。
- **定位**：独立进程按 Manager 同构代码复现——真实凭证下 `OnStarted` 触发、`Start()` 随即返回；并用库内凭证直连网关换票 200（凭证/网络均正常，排除钉钉侧）。
- **修复**：删除 RunTask 的 `finally` 置离线，`IsOnline` 完全由 `OnStarted`/`OnStoped` 事件维护（启动任务仅观察取消与启动阶段异常；凭证错误时 SDK 停在内部重试循环、Start 不返回，仍正确显示离线）。
- **验证**：修复后事件模式独立复现 `isOnline` 置 true 后保持；`dotnet build` 0 error；`node local-dev/dingtalk-e2e.mjs` 39/39（假凭证路径不受影响）；启动日志「钉钉长连接初始化完成，共 2 个应用」。

## 证据摘要（2026-10-26，修复：机器人消息已读但不回复——msgtype 字段名大小写）

- **现象**：真实钉钉机器人消息显示已读但无任何回复、无会话落库、无 Redis 映射（@DT-S19 首次真实环境验证暴露）。
- **定位**：Debug 级抓包实例接管连接捕获真实推送——转发器全链正常（收到推送 → appExists → chatbotUserId 回填 UPDATE → 绑定查询均有日志），之后所有分支日志（未绑定/无处理器/执行失败/webhook 缺失）均未出现 → handler 走了静默返回；从抓包日志还原真实 data JSON，发现线上字段为 **`"msgtype"`（全小写）**，而 `AppDingTalkMessageHandler.TryParse` 读的是 `"msgType"`（驼峰，误信官方 SDK 的 C# 属性名 `MsgType`）→ MessageType 取空 → 命中「非文本消息」静默分支。
- **修复**：`TryParse` 改为 `msgtype`（兼容 `msgType` 双写）；同轮顺带核实 `conversationId`/`text.content`/`sessionWebhook`/`chatbotUserId` 字段名与真实推送一致。
- **验证**：`dotnet build` 0 error；带修复实例接管连接后待用户真实消息回归（handler 应建会话 + 经 sessionWebhook 回复）。
- **回归再暴露出站缺陷（同日）**：msgtype 修复后真实消息走通到「会话创建 + Agent 运行 + webhook 回复」，但钉钉返回 `msgtype is null`——`DingTalkApiClient` 的 webhook 回复体漏了 `"msgtype":"text"` 字段（只有 text 节点）。补上后重部署，待真实消息回归。两处缺陷同源：对钉钉 API 的 msgtype 字段（入站全小写、出站必填）均未核对线上契约。

| @DT-S29 | 双实例实测（5002/5006 新构建）：A 圈占 31 应用后触发公平份额释放 14 个、B 争抢到 14 个（≈16/14 负载均衡）；杀 A 后 B 约 40s 争抢接管全部应用并重连真实连接（日志观测） | PASS（2026-10-26） |
| @DT-S30 | 同上双实例实测（对账周期 5s）+ ApplyCreate 本地争抢即建连 | PASS 走查（2026-10-26） |
