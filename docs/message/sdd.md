# 站内信与事件推送（Message，MSG）设计规格（SDD）

> 关联：[SDD](./sdd.md) ｜ [BDD](./bdd.md)（@MSG-Sxx） ｜ [TDD](./tdd.md) ｜ [SOP](./sop.md) ｜ 上游：[../team/sdd.md](../team/sdd.md)（成员解析）、[../auth/sdd.md](../auth/sdd.md)（JWT 鉴权） ｜ 证据：[local-dev/message-e2e.mjs](../../local-dev/message-e2e.mjs)、[tests/MoAI.Message.Tests](../../tests/MoAI.Message.Tests)
> 规范：[../DOC-STANDARD.md](../DOC-STANDARD.md)。行为场景见 BDD，本文不重复。

## 目标

两层能力，同一个模块：

1. **平台级 SignalR 事件推送系统**：`/hub/moai` 单一 Hub，把「发生了什么」实时推给在线用户。**只推事件不推数据**——事件负载只含事件名与标识字段，消费方收到后调对应 REST 接口取详情，推送层与业务数据零耦合。
2. **站内信**：第一個事件源。领域服务落库（每收件人一行，独立已读态）→ RabbitMQ 异步 → 后台消费者触发 SignalR 推送 `message.new` 事件；离线用户不丢消息（落库为准），上线后接口拉取兜底。

其它模块发送站内信或推送自定义事件，只需引用 `MoAI.Message.Shared`。

## 组件（源码树）

```
src/message/
  MoAI.Message.Shared/       MessageSharedModule
    Models/                  MessageType、MessageSendItem、EventPushItem、EventNames（事件名注册表）
    Services/                IMessageService（站内信领域服务）、IEventPushService（通用事件推送）
    Events/                  MessagePushMessage（[RouterKey("message.push")]，队列名常量 QueueName）
    Commands/Queries/        ReadMessage/ReadAllMessage/DeleteMessage、QueryMessageList/QueryMessageUnreadCount（+Responses）
  MoAI.Message.Core/         MessageCoreModule
    Services/                MessageService、EventPushService（[InjectOnScoped]）
    Consumers/               MessagePushConsumer（[Consumer("message.push", Qos=1)] → IHubContext 推送）
    Handlers/                五个 CQRS Handler
  MoAI.Message.Api/          MessageApiModule
    Controllers/             MessageController（/api/message/*，个人数据仅 [Authorize]）
    Hubs/                    MoAIEventHub（/hub/moai）、MoAIUserIdProvider（ClaimTypes.NameIdentifier）
src/database/.../Entities/MessageEntity.cs + Database.Postgres/Data/MessageConfiguration.cs（幂等 DDL 见 asserts/message.sql）
src/MoAI/Modules/ConfigureSignalRModule.cs（AddSignalR + Redis backplane + UserIdProvider 注册）
src/MoAI/Modules/ConfigureAuthorizaModule.cs（JwtBearer OnMessageReceived：/hub 前缀读 access_token 查询参数）
tests/MoAI.Message.Tests/    领域服务与读侧 Handler 单测（sqlite 内存库，照 Org.Tests 手法）
```

调用链：业务模块 → `IMessageService.SendToUsersAsync/SendToTeamAsync/SendToAllUsersAsync` 落库 → `IEventPushService.PushToUsersAsync` 发布 MQ → `MessagePushConsumer` → `Clients.Users(ids).SendAsync("onEvent", EventPushItem)` →（Redis backplane）→ 目标用户全部在线连接。

## 数据模型

`message`（`MessageEntity`，IFullAudited 软删除，**每收件人一行**）：

| 列 | 说明 |
|---|---|
| id | bigint identity（自增序即发送序，列表按 id 倒序 = 时间倒序） |
| recipient_user_id | 接收人，列表/未读数/读写全部按它隔离 |
| title / content | ≤256 / ≤4000，纯文本 |
| message_type | MessageType 枚举（system=1、team=2，可扩展） |
| source | 来源模块标识（如 publication），空为平台 |
| sender_user_id | 发送人，空为系统（列表「创建人」列语义=发送人，FillAsync 填名） |
| params | 可选 JSON 文本，供前端跳转定位 |
| is_read / read_time | 个人已读态（WhereUpdateAsync 批量盖章） |
| 审计列 + is_deleted | 框架注入；删除走 `SoftDeleteAsync`（is_deleted 写非零雪花 id） |

索引：`(recipient_user_id, is_read)`、`(recipient_user_id, create_time desc)`，均 filter `is_deleted=0`。存量库由 `DatabasPostgresModule` 幂等 DDL 补表。

## 关键决策

1. **只推事件不推数据**：`EventPushItem { event, data?, time }`，`data` 仅允许标识类字段（id/类型名），禁止业务正文；事件名小写点分，统一登记在 `EventNames`（首个：`message.new`）。理由：推送层不随业务数据模型演进，耦合与隔离最优。
2. **RabbitMQ 异步解耦**：落库成功后才发 `MessagePushMessage`；消费者 Qos=1，重试耗尽 Fallback 直接 Ack 放弃（推送尽力而为，不投死信，离线由拉取兜底），避免毒消息堆积。
3. **多实例状态共享**：官方 `Microsoft.AspNetCore.SignalR.StackExchangeRedis` backplane，ChannelPrefix `moai:sr` 与平台 Redis key 前缀一致；连接→用户归属由 `MoAIUserIdProvider`（JWT sub）决定，`Clients.User(id)` 天然覆盖同用户多端。
4. **Hub 鉴权**：浏览器 WebSocket 无法带 Authorization 头，`JwtBearerEvents.OnMessageReceived` 仅对 `/hub` 前缀路径从 `access_token` 查询参数取 token（官方推荐做法），Hub `[Authorize]`。
5. **每收件人一行**：个人已读/删除天然独立，无回执表；团队/全员发送在发送时解析为具体用户 id（团队成员走 `ITeamService.GetMemberUserIdsAsync`，全员取未禁用用户）。
6. **UseHttpLogging 与 Hub 冲突**：请求体缓冲会让 SignalR 握手读到空输入（HandshakeFailed），Hub 标注 `[HttpLogging(HttpLoggingFields.None)]` 规避，见 SOP 排障表。
7. **列表排序用 id 倒序**：identity 自增即发送序，规避各库对 timestamptz ORDER BY 的方言差异，单测 sqlite 可跑。
8. 领域服务空收件人/超长内容抛 `BusinessException`（400）；`SendToTeamAsync` 团队不存在抛 404；成员为空静默返回（无收件人无消息）。

## 已知问题

- 站内信暂无自动清理（保留策略/Hangfire 定期归档待后续轮次）。
- 管理员群发接口待后续按需补充；前端已接入（SignalR 客户端 + 侧边栏角标 + 右上角 Notification + 个人中心二级菜单「消息中心/账号设置分区」，见 `ui/src/realtime/eventPush.ts`、`ui/src/pages/account/`）。
- 多实例跨节点推送已具备（backplane），部署级双实例验证步骤见 SOP。
- `data` 字段为标识字典（string→string），复杂事件负载需扩展时再评估。
