# 站内信与事件推送（Message，MSG）运维手册（SOP）

> 关联：[SDD](./sdd.md) ｜ [BDD](./bdd.md) ｜ [TDD](./tdd.md) ｜ [SOP](./sop.md)
> 规范：[../DOC-STANDARD.md](../DOC-STANDARD.md)。

## 1. 数据库升级

`message` 为后加表：新库由 `EnsureCreated` 建表，存量库由 `DatabasPostgresModule` 启动时幂等 DDL 补齐（与 [asserts/message.sql](../../asserts/message.sql) 等价），无需手工执行。

## 2. 其它模块如何接入（开发向）

1. 项目引用 `src/message/MoAI.Message.Shared/MoAI.Message.Shared.csproj`。
2. 注入 `IMessageService` 发站内信（`SendToUsersAsync` / `SendToTeamAsync` / `SendToAllUsersAsync`），或注入 `IEventPushService` 推自定义事件。
3. 事件名登记进 `MoAI.Message.Models.EventNames`（小写点分），`data` 只放标识字段；前端/客户端按约定收到 `onEvent` 后调对应 REST 接口取详情。
4. Handler 禁注入 `IUserContextProvider`，用户维度走 `IUserIdContext`（见 [cqrs-conventions](../cqrs-conventions.md)）。

## 3. 排障表

| 现象 | 可能原因与处置 |
|---|---|
| negotiate 401 | 未带 token；WebSocket 场景须以 `?access_token=<JWT>` 传递（ConfigureAuthorizaModule 仅对 /hub 前缀读取） |
| 浏览器 negotiate 瞬间 `Failed to fetch`（REST 正常） | SignalR 默认 `withCredentials: true` 与 `AllowAnyOrigin` CORS 冲突：前端 eventPush.ts 已设 `withCredentials: false`（token 鉴权不依赖 cookie），勿移除 |
| 连接后握手失败 `An unexpected error occurred during connection handshake` | 请求被中间件缓冲：Hub 已标注 `[HttpLogging(HttpLoggingFields.None)]`，勿移除；新增全局读请求体的中间件时需为 /hub 放行 |
| 事件收不到（连接正常） | 依次检查：RabbitMQ 队列 `message.push` 是否有消费者（管理口 Queues 页）→ 消费者日志（MessagePushConsumer）→ Redis backplane 连接（启动日志 RedisHubLifetimeManager Connecting to Redis endpoints） |
| 前端首连失败后不再重连 | eventPush.ts 已内置 15s 定时重试（`withAutomaticReconnect` 只覆盖建立后的断线），勿移除 |
| 多套环境共用一个 Redis 串台 | backplane ChannelPrefix 固定 `moai:sr`，多环境需隔离 Redis 实例或调整 ConfigureSignalRModule 前缀 |
| 消息落库了但没推送 | 推送在 MQ 异步链路，查 `message.push` 队列堆积与消费者存活；重试耗尽会放弃（尽力而为语义），离线用户上线后由接口拉取兜底 |
| 未读数/全部已读不生效（拿到别人或 0） | 命令必须作为 Controller **动作参数绑定**（`[FromQuery]`/`[FromBody]`），在方法体内 `new` 命令会绕过 `AutoAssignUserIdFilter` 丢失 ContextUserId |

## 4. 多实例部署级验证（双实例示例）

1. 两实例共用同一 Postgres/Redis/RabbitMQ（backplane 与 MQ 均依赖共享中间件）。
2. 用户 A 的连接落在实例 1（看实例日志 negotiate）。
3. 向实例 2 触发一次发送（或在实例 2 所在进程调用领域服务）。
4. 预期：实例 2 消费 MQ → 经 backplane 转发 → A 在实例 1 的连接收到 `onEvent`。也可用管理口向 `message.push` 发布测试事件复现 @MSG-S6。

## 5. 前端接入要点（开发向）

- 连接生命周期挂 `ui/src/layouts/useEventPush.ts`（AppLayout 内），随 accessToken 建立与断开；业务页用 `onEventPushEvent(handler)` 订阅事件（如消息页在线刷新）。
- 事件只推标识：前端收到 `message.new` 后调 `getMessages({pageNo:1,pageSize:1})` 取最新一条拿标题弹 Notification（`feedback.notify`，duration=0 悬浮）。
- 未读角标数据源 `ui/src/store/notification.ts`，登录拉取 + 事件与已读/删除操作后 `refresh()`。

## 6. 验收流程

1. 构建：`dotnet build src/MoAI/MoAI.csproj` 0 error；`dotnet test tests/MoAI.Message.Tests` 全绿；`cd ui && npm run typecheck && npm run lint && npm run test` 全绿。
2. 后端运行中执行 `node local-dev/message-e2e.mjs`，全部 PASS（MQ 场景在管理口不可达时 SKIP）。
3. 浏览器走查：登录 → 左下角铃铛角标 → MQ 发事件 → 通知弹出 + 角标刷新 → 消息中心（二级菜单/过滤/已读/删除）→ 刷新页面 toast 消失角标回显。
4. 对照 [TDD](./tdd.md) 映射表确认场景覆盖；场景定义见 [BDD](./bdd.md)（@MSG-Sxx）。
