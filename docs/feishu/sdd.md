# feishu 飞书通知模块 设计规格（SDD）

> 关联：[SDD](./sdd.md) ｜ [BDD](./bdd.md) ｜ [TDD](./tdd.md) ｜ [SOP](./sop.md) ｜ 上游：[../team/sdd.md](../team/sdd.md)（团队角色门禁） ｜ [../ai/sdd.md](../ai/sdd.md)（Agent 运行时） ｜ 证据：[local-dev/feishu-e2e.mjs](../../local-dev/feishu-e2e.mjs)、[asserts/feishu_app.sql](../../asserts/feishu_app.sql)
> 规范：[../DOC-STANDARD.md](../DOC-STANDARD.md)。行为场景见 BDD（@FS-Sxx），本文不重复。

## 目标

复用飞书长连接的团队级通知基座：**同一个飞书应用（AppID）只建一条 WebSocket 长连接**，事件按绑定关系转发到业务模块，避免多处各建连接互相挤掉线、同一事件被多处消费。

当前阶段落地**应用渠道**：飞书用户在群聊/私聊中发消息给绑定的团队应用 → 运行该应用的 Agent → 回复发回原会话。知识库（飞书文档变更→重新向量化）后续单独模块设计，届时扩展 `FeishuChannelType` 枚举即可接入本基座。

底层长连接能力来自 NuGet 包 `Maomi.FeishuWss`（[maomi.feishu](https://github.com/whuanle/maomi.feishu)，.NET 复刻 oapi-sdk-go/v3/ws）：endpoint 拉取 → WSS 持久连接 → protobuf 帧 → 自动重连。

## 组件

```
src/feishu/                       通知基座（本模块）
├── MoAI.Feishu.Shared/           Commands/Queries + Models/FeishuEventMessage + Services 契约
├── MoAI.Feishu.Core/             Handlers(6) + Queries(1) + Services(连接管理/转发/凭证/API客户端/宿主)
└── MoAI.Feishu.Api/              Controllers/FeishuAppController（/api/feishu_app）
src/ai/MoAI.AI.Core/              应用渠道消费者
└── Services/AppFeishuMessageHandler.cs   im.message.receive_v1 → Agent 会话 → 回复
```

基座核心服务（`MoAI.Feishu.Core/Services/`）：

| 服务 | 生命周期 | 职责 |
|---|---|---|
| `FeishuConnectionManager` | Singleton | 每个启用的 feishu_app 维持一条 `WssClient` 长连接；启动全量加载，增删改后 Apply* 同步连接；`IsOnline` 供列表查询 |
| `FeishuEventForwarder` | Singleton | 收帧线程只做 envelope 解析 + event_id 去重（30 分钟内存窗口）后立即 ack；异步查绑定并按渠道类型投递 |
| `FeishuConnectionHostedService` | Hosted | 启动建连/停机断连；初始化失败仅记日志不阻断宿主 |
| `FeishuAccessTokenProvider` | Singleton | tenant_access_token 获取与缓存（提前 5 分钟过期），实现 `IFeishuAccessTokenProvider` |
| `FeishuApiClient` | Singleton | 回调飞书开放平台（发文本消息），实现 `IFeishuApiClient` |

业务模块接入契约（`MoAI.Feishu.Shared/Services/`）：实现 `IFeishuEventHandler`（声明 `ChannelType`，内部按 `EventType` 过滤并自捕获异常）并在提供方模块注册（`AiCoreModule` 注册 `AddSingleton<IFeishuEventHandler, AppFeishuMessageHandler>()`）。事件消息为 `FeishuEventMessage`（事件原文在 `Payload`）。

## 应用渠道回复链路（AppFeishuMessageHandler）

`MoAI.AI.Core` 实现（引用 Feishu.Shared 契约，避免基座反向依赖 AI 运行时），与 AG-UI 共用同一套应用 Agent 管线：

1. **过滤**：只处理 `im.message.receive_v1`；`sender_type=user`（忽略机器人防回路）；`message_type=text`；提取 `content.text` 并去掉 `@_user_N` 占位符（群聊 @机器人 场景）。
2. **应用门禁**：绑定的 app 必须存在、未禁用、内部应用、Agent 类型且已发布，否则静默忽略（记日志）。
3. **会话定位**：按「飞书应用 + chat_id」在 Redis（`feishu:chat:{feishuAppId}:{chatId}`，30 天滑动）映射到 `app_agent_session`；未命中则新建会话（`UserType=External`、标题由落库时按首条用户消息生成；`CreateUserId=0` 后台审计）。映射丢失自愈为新建会话。
4. **运行**：`AppAgentFactory.CreateAsync(appId, teamId, userId:0, sessionId, isDebug:false)` 装配 Agent（含插件/知识库/沙箱工具与压缩管线），加载热态快照（与 `AppAgentSessionStore` 同一逻辑）→ `RunAsync` → 提取最后一条 Assistant 文本。
5. **回复与落库**：回复经 `IFeishuApiClient.SendTextMessageAsync` 发回原 chat_id（截断 3000 字符）；会话序列化回热态 + `AppChatFlushService.FlushAsync` 落库（消息、token 聚合、冷快照）。业务异常（如未配置模型）把异常消息作为回复返回给用户。
6. **串行化**：同一 chat_id 用进程内信号量串行，避免并发写历史错序；用量计入团队/应用维度（userId=0）。

## 数据

- `feishu_app`（[asserts/feishu_app.sql](../../asserts/feishu_app.sql)）：飞书应用连接。`app_id` 唯一（未删除行）——同一飞书开放平台应用全局只能建一条连接，否则两条连接互踢。
- `feishu_app_binding`：渠道绑定。**核心互斥约束**：`feishu_app_id` 唯一（未删除行）——同一飞书应用同时只能绑定一个渠道，杜绝两个渠道同时消费同一事件；反向索引 `(channel_type, channel_id)` 供渠道侧反查绑定。
- 渠道用 `(FeishuChannelType, string ChannelId)` 弱关联（当前仅 `app` → app.id），绑定 Handler 校验渠道存在且与连接同团队。
- 权限模型沿用团队资源：连接属于团队，写操作（增删改/绑定/解绑）Admin+，读列表 Member+。

## 关键决策

1. **连接复用而非转发总线**：每 AppID 一条连接由飞书模块独占；业务模块不感知 WSS，只消费 `IFeishuEventHandler`。
2. **绑定互斥在 Handler + 数据库双层兜底**：Handler 查重返回 409，唯一过滤索引防并发窗口。
3. **收帧线程零阻塞**：转发器立即 ack，业务处理转线程池；飞书侧重发由 event_id 去重器吸收。
4. **禁用即断连**：`is_disable=true` 断开且不重连；事件到达时若应用已禁用/删除，转发器二次校验后丢弃。
5. **删连接级联解绑**：删除 feishu_app 同时软删其绑定，渠道可立即被其它连接绑定。
6. **回复走既有 Agent 会话管线**：飞书会话与 AG-UI 会话同构（热态/落库/压缩/工具全复用），不另建存储；chat↔session 映射放 Redis（30 天滑动）而非加列。
7. **飞书用户不映射内部用户**：`userId=0`，会话 `UserType=External`；用量计入团队/应用维度。

## 已知问题

- 凭证错误（如 app_id invalid）时 SDK 终态退出并记日志，`IsOnline=false`；不做自动重建（重新保存连接即重连）。
- 去重窗口为单实例内存（30 分钟/4096 条），多实例部署时各实例独立去重，幂等仍需业务侧保障。
- 仅支持文本消息；非文本（图片/富文本）静默忽略。Agent 回复为 Markdown 原文，按飞书 text 消息发送（Markdown 语法原样显示）。
- chat↔session 映射 Redis 丢失后自愈为新建会话（旧会话成为孤儿但不再被使用）；进程内串行锁不跨实例。
- Lark 国际版需在创建时显式填 `domain=https://open.larksuite.com`。
