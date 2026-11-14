# dingtalk 钉钉渠道模块 设计规格（SDD）

> 关联：[SDD](./sdd.md) ｜ [BDD](./bdd.md) ｜ [TDD](./tdd.md) ｜ [SOP](./sop.md) ｜ 上游：[../team/sdd.md](../team/sdd.md)（团队角色门禁） ｜ [../ai/sdd.md](../ai/sdd.md)（Agent 运行时） ｜ 同构模块：[../feishu/sdd.md](../feishu/sdd.md) ｜ 证据：[local-dev/dingtalk-e2e.mjs](../../local-dev/dingtalk-e2e.mjs)、[asserts/dingtalk_app.sql](../../asserts/dingtalk_app.sql)
> 规范：[../DOC-STANDARD.md](../DOC-STANDARD.md)。行为场景见 BDD（@DT-Sxx），本文不重复。

## 目标

让钉钉用户与团队应用对话：**一个钉钉开放平台企业内部应用（AppKey/AppSecret）只建一条 Stream 模式长连接**，机器人消息（单聊/群聊 @机器人）按绑定关系转发到业务模块，运行应用 Agent 后把回复发回原会话。整体与 [../feishu/sdd.md](../feishu/sdd.md) 飞书模块同构（连接管理/转发/绑定/门禁/会话管线一致），差异在传输层与回复通道。

底层长连接能力来自钉钉官方 NuGet 包 `DingTalkStream.Core` + `DingTalkStream.Robot`（2026.7.21）：网关换票（`POST https://api.dingtalk.com/v1.0/gateway/connections/open`）→ WebSocket 持久连接 → 自动重连；机器人消息回调 Topic 为 `/v1.0/im/bot/messages/get`（CALLBACK 型，5 秒内需回复 ack，否则服务端重发）。**Stream 模式无需公网地址与加解密配置**。回复走推送体内的会话 webhook（单聊/群聊通用，免 access_token 管理）。

## 组件

```
src/dingtalk/                      钉钉渠道基座（本模块）
├── MoAI.DingTalk.Shared/         Commands/Queries + Models/DingTalkEventMessage + Services 契约
├── MoAI.DingTalk.Core/           Handlers(3) + Queries(1) + Services(连接管理/转发/去重/API客户端/宿主)
└── MoAI.DingTalk.Api/            Controllers/DingTalkAppController（/api/dingtalk_app）
src/ai/MoAI.AI.Core/              应用渠道消费者
└── Services/AppDingTalkMessageHandler.cs   机器人消息 → Agent 会话 → webhook 回复
```

基座核心服务（`MoAI.DingTalk.Core/Services/`）：

| 服务 | 生命周期 | 职责 |
|---|---|---|
| `DingTalkConnectionManager` | Singleton | 每个启用的 dingtalk_app 维持一条 `DingTalkStreamClient` 长连接（订阅 CALLBACK + 机器人 Topic）；启动全量加载，增删改后 Apply* 同步连接；`IsOnline` 供列表查询 |
| `DingTalkEventForwarder` | Singleton | 收帧线程只做 ack 回复 + message_id 去重（30 分钟内存窗口）后立即返回；异步查绑定并按渠道类型投递；从推送体提取会话 webhook |
| `DingTalkEventDeduplicator` | Singleton（被转发器持有） | message_id 内存窗口去重，吸收钉钉侧重发 |
| `DingTalkConnectionHostedService` | Hosted | 启动建连/停机断连；初始化失败仅记日志不阻断宿主 |
| `DingTalkApiClient` | Singleton | 会话 webhook 回复（`{msgtype:"text",text:{content}}`，errcode≠0 转 BusinessException），实现 `IDingTalkApiClient` |

业务模块接入契约（`MoAI.DingTalk.Shared/Services/`）：实现 `IDingTalkEventHandler`（声明 `ChannelType`，内部按 `Topic` 过滤并自捕获异常）并在提供方模块注册（`AiCoreModule` 注册 `AddSingleton<IDingTalkEventHandler, AppDingTalkMessageHandler>()`）。事件消息为 `DingTalkEventMessage`（推送原文在 `Payload`，会话 webhook 单列）。

### 前端入口：应用工作台「外部渠道」钉钉分区

`ui/src/pages/teams/apps/AppDingTalkChannelsSection.tsx`（`ui/src/api/dingtalkApp.ts` 封装 Kiota），与飞书分区（`AppChannelsSection.tsx`）并排渲染于应用工作台「外部渠道」页，仅内部 Agent 应用的 Admin+ 可见。交互与飞书分区一致：**只提供「接入钉钉应用」（创建连接即绑定到当前应用）、停用/启用、删除三种操作**，删除即彻底移除（删除后同 AppKey 可重新接入）；表单仅 AppKey（Client ID）/AppSecret（Client Secret）/名称/描述——钉钉无 Lark 式多域名，无需域名字段。渠道表格只回显 `bindChannelId == appId` 的连接。

## 应用渠道回复链路（AppDingTalkMessageHandler）

`MoAI.AI.Core` 实现（引用 DingTalk.Shared 契约），与 AG-UI、飞书渠道共用同一套应用 Agent 管线：

1. **过滤**：只处理 Topic `/v1.0/im/bot/messages/get`；`msgType=text`；提取 `text.content` 并去掉群聊 `@机器人名 ` 前缀（钉钉正文可能带 @ 前缀文本）。钉钉不回推机器人自身消息，无回路问题。
2. **应用门禁**：绑定的 app 必须存在、未禁用、内部应用、Agent/Workflow 类型且已发布，否则静默忽略（记日志）。
3. **发送者识别**：解析推送体 `senderId`（即 unionId，跨应用一致）与 `senderStaffId`，经 `ChannelUserMatchService` 反查 `user_oauth_connection`（钉钉 OAuth 绑定记录 `sub` 即 unionId）：命中唯一且用户可用则会话归属该内部用户，否则匿名（`userId=0`）。见关键决策 6。
4. **会话定位**：按「钉钉应用 + conversationId（归属时追加 `:u{userId}`）」在 Redis（`dingtalk:chat:{dingTalkAppId}:{conversationId}`，30 天滑动）映射到 `app_agent_session`；未命中则新建会话（归属用户时 `CreateUserId=用户id、UserType=Normal`，匿名时 `UserType=External`）。映射丢失自愈为新建会话。
5. **运行**：`AppAgentFactory.CreateAsync(appId, teamId, userId:(归属或0), sessionId, isDebug:false)` 装配 Agent（含插件/知识库/沙箱工具与压缩管线；Workflow 应用由工厂内部转流程执行返回最终回复），加载热态快照 → `RunAsync` → 提取最后一条 Assistant 文本。
6. **回复与落库**：回复经 `IDingTalkApiClient.SendTextMessageAsync` 发回推送体携带的会话 webhook（截断 3000 字符）；会话序列化回热态 + `AppChatFlushService.FlushAsync` 落库。业务异常（如未配置模型）把异常消息作为回复返回给用户。
7. **串行化**：同一 conversationId 用进程内信号量串行。

## 数据

- `dingtalk_app`（[asserts/dingtalk_app.sql](../../asserts/dingtalk_app.sql)）：钉钉应用连接。`app_key` 唯一（未删除行）——同一钉钉应用全局只能建一条连接（一应用一 Stream 订阅，多处建连互踢）。`chatbot_user_id` 存机器人的 dingtalk id（回调报文 `chatbotUserId`，加密串），由事件转发器在收到首条机器人消息时回填（已一致不写库）；应用模块据此生成「拉起机器人单聊」的 jumprobot applink（钉钉无按 AppKey 拉起的稳定链接）。
- `dingtalk_app_binding`：渠道绑定。**应用渠道独占**：`(dingtalk_app_id) WHERE is_deleted=0 AND channel_type=0` 唯一——同一钉钉应用只能绑定一个团队应用，否则同一条机器人消息被两个应用同时消费；另建 `(dingtalk_app_id, channel_type, channel_id)` 唯一防重复绑定。
- 渠道用 `(DingTalkChannelType, string ChannelId)` 弱关联（`app` → app.id），绑定校验渠道存在且与连接同团队。当前枚举仅 `App=0`；后续钉钉订阅型渠道（如钉钉文档外部源）扩展枚举即可复用分层互斥模型（参考飞书 `feishu_app_wiki_channel.sql`）。
- 权限模型沿用团队资源：写操作 Admin+，读列表 Member+。接入 = `POST /api/dingtalk_app` 携带 `channelType=app` + `channelId` 创建即绑定（无独立绑定端点），删除即级联解绑。

## 多实例部署（按应用粒度的 Redis 租约负载均衡）

钉钉 Stream 网关同一应用允许多连接并存并**对消息负载均衡**（实测：两实例并挂时 4 条消息仅 1 条进入其中一侧），多实例各建连接会导致消息被分摊到不同实例、处理状态割裂。解决方案为**按应用粒度的 Redis 租约抢占**（共享实现 [MoAI.Database.Shared/Redis/RedisAppLease.cs](../../src/database/MoAI.Database.Shared/Redis/RedisAppLease.cs)，飞书模块同构复用）：每个 dingtalk_app 一个独立租约键 `leader:dingtalk-conn:{id}`，各实例的**对账循环**（5s/周期）持续：

1. **续租**已持有的连接（GET 校验持有者后 EXPIRE，30s 租约）；续租发现易主（本实例曾停顿超 30s）即断开，应用由持有实例处理。
2. **争抢**未持有的应用租约（打乱顺序，先到先得）——应用自然分散到不同实例，实现负载均衡。
3. **按数据库对账**：新增/删除/禁用/凭证变化（含其它实例发起的变更）最多一个周期（5s）自动同步；本实例的 Apply* 仅在持有连接时立即生效。
4. **公平份额再均衡**：实例心跳注册（15s 未刷新视为下线），持有数超过 `ceil(应用数/存活实例数)+1` 的实例主动释放多余连接（租约释放后由其它实例争抢）——解决「先启动实例在其它实例就绪前圈占全部应用」的冷启动竞争（实测 31/14 → 两实例各持 ~16）。
5. **在线状态跨实例**：持有者把连接状态写入 Redis 哈希 `connstate:dingtalk-connection`（5s 刷新、60s 过期），非持有实例的 `IsOnline` 查询读哈希，避免列表误显示离线。
6. **优雅停机**：先断开本实例连接并释放租约，其它实例立即争抢接管。

故障转移：持有者下线 → 其应用租约 30s 内过期 → 其它实例 ≤5s 争抢建连，全程 ≈ 35s；接管窗口内未被 ack 的推送由钉钉重发、message_id 去重吸收。未引入 MQ（Maomi.MQ）：应用分散后各实例就地处理即可，未来要集中处理可让转发器改发 MQ。单实例部署行为不变（全部应用租约都归它）。

- **后台开销（性能预算）**：每实例每对账周期（5s）固定为 1 条数据库查询 + 常数次 Redis 网络往返（续租/争抢/状态写入均按流水线批量发送 WhenAll，往返次数与连接数无关；状态哈希仅写变化字段），Redis 操作量 ≈ 3×持有应用数/5s——千级应用下单周期耗时仍在数十毫秒级。真正的常驻成本是每个应用一条长连接（内存 + 心跳帧），已按应用租约分散到各实例；凭证错误的连接为终态退出不重试（不产生持续重试流量）。

## 关键决策

1. **与飞书同构而非复用**：钉钉建独立 `src/dingtalk` 三层模块，结构、绑定模型、门禁与会话管线与飞书一致；不抽象公共基座——两平台传输层（WSS/Stream）、凭证体系（tenant_token/webhook）、事件形态差异大，强行统一只会加间接层。
2. **回复走会话 webhook 而非机器人 REST API**：推送体自带 webhook（单聊/群聊通用、预签名），免 access_token 获取与缓存；即时回复场景下有效期足够。后续若需主动推送（会话外触达）再引入 `robot/oToMessages/batchSend` 等接口。
3. **收帧线程同步 ack**：SDK 的 `Reply` 即对同一 WebSocket 发帧，在收帧线程上同步发送与系统消息回复天然串行（避免并发写 WebSocket）；业务处理转线程池，钉钉侧重发由 message_id 去重器吸收。
4. **禁用即断连**：`is_disable=true` 断开且不重连；推送到达时若应用已禁用/删除，转发器二次校验后丢弃。
5. **假凭证优雅降级**：Stream 网关返回 `authFailed` 时 SDK 内部重试仅记日志，不影响宿主与其它连接；修正凭证保存即重连。
6. **发送者可识别则归属、否则匿名**：识别键 = 推送体 `senderId`（unionId）+ `senderStaffId`，钉钉 OAuth 绑定记录 `sub` 即 unionId，跨应用天然一致；命中多个不同账号、账号停用/删除一律降级匿名。归属会话 `CreateUserId=用户id、UserType=Normal`（网页端可见可续聊），匿名会话保持 `userId=0、UserType=External`；归属会话的 Redis 键带 `:u{userId}` 后缀（群聊中各绑定成员独立会话）。与飞书渠道同构，见 [../feishu/sdd.md](../feishu/sdd.md)。

## 已知问题

- 仅支持文本消息；非文本（图片/文件/音频）静默忽略。Agent 回复为 Markdown 原文，按钉钉 text 消息发送（Markdown 语法原样显示）。
- 会话 webhook 有有效期（推送体 `sessionWebhookExpiredTime`）；Agent 运行超过有效期后回复失败（即时对话场景不会发生），未做 REST API 兜底。
- 去重窗口为单实例内存（30 分钟/4096 条），多实例部署各实例独立去重，幂等需业务侧保障；进程内串行锁不跨实例。
- 连接「在线」语义为 Stream 生命周期已启动（SDK 仅暴露 OnStarted/OnStoped，不暴露单次断连事件），断线重连间隙仍显示在线。
- 群聊中机器人只在被 @ 时收到消息（钉钉平台行为）；应用列表页「外部对话入口」（`AppItem.externalChannels`）钉钉渠道绑定即露出，`chatbot_user_id` 回填前 chatUrl 用 AppKey（robotCode）兜底、回填后自动切换为精确链接（钉钉无按 AppKey 拉起机器人会话的官方稳定深链，兜底为尽力尝试），详见 [@DT-S27](./bdd.md#dt-s27)。
