# 通知中心（Notification，NT）设计规格（SDD）

> 关联：[SDD](./sdd.md) ｜ [BDD](./bdd.md)（@NT-Sxx） ｜ [TDD](./tdd.md) ｜ [SOP](./sop.md) ｜ 上游：[../message/sdd.md](../message/sdd.md)（站内信）、[../settings/sdd.md](../settings/sdd.md)（SMTP 邮件服务） ｜ 证据：[local-dev/notification-e2e.mjs](../../local-dev/notification-e2e.mjs)
> 规范：[../DOC-STANDARD.md](../DOC-STANDARD.md)。行为场景见 BDD，本文不重复。

## 目标

统一承接平台各类通知出口（飞书/钉钉/企业微信机器人 webhook、Telegram Bot、通用 webhook、SMTP 邮件、站内推送），**业务方只调统一领域服务接口，不感知任何外部接口格式**。解决「每个需要通知的场景各自配一套渠道」的碎片化：渠道集中配置、集中测试、集中留痕（发送记录），子系统后续接入复用同一模块。

## 组件（源码树）

```
src/notification/
  MoAI.Notification.Shared/      NotificationSharedModule
    Models/                      NotificationMessage（统一消息：Title/Content/Level/Source）、
                                 NotificationSendOptions（ChannelIds 定向）、NotificationChannelSendResult、
                                 NotificationChannelConfigs（七类渠道配置 POCO + JSON 解析/校验）
    Services/                    INotificationService（领域服务唯一入口）
    Commands/Queries/            渠道 CRUD/测试/广播 + 渠道列表/发送记录分页（+Responses）
  MoAI.Notification.Core/        NotificationCoreModule（注册 HttpClient + 七个发送器）
    Services/                    NotificationService（[InjectOnScoped]，NotifyAsync=Hangfire 异步 / SendNowAsync=同步）、
                                 NotificationChannelDispatcher（按类型路由发送器、计时、异常归一）、
                                 NotificationSendJob（Hangfire 一次性任务，[AutomaticRetry(3)]）
    Senders/                     INotificationChannelSender + 七实现：Feishu/DingTalk/Wecom webhook、
                                 TelegramBot、GenericWebhook（自定义头+模板）、SmtpChannel（复用 IEmailSenderService）、
                                 InSiteChannel（复用 IMessageService）；NotificationText（纯文本/Markdown 渲染）、SenderHttp
    Handlers/Queries/            六个 CQRS Handler
  MoAI.Notification.Api/         NotificationApiModule
    Controllers/                 NotificationController（/api/notification/*，全部管理员门禁）
src/database/.../Entities/Notification{Channel,Log}Entity.cs + Database.Postgres/Data/*Configuration.cs（幂等 DDL 见 asserts/notification.sql）
ui/src/pages/admin/NotificationCenter.tsx + ui/src/api/notification.ts（渠道管理/发送记录双 Tab 管理页）
```

调用链：业务模块 → `INotificationService.NotifyAsync` → 解析启用渠道（可 ChannelIds 定向）→ 每渠道落 `notification_log`（Pending）→ Hangfire Enqueue → `NotificationSendJob` → `NotificationChannelDispatcher` → 渠道发送器 → 外部接口；终态回写 log（success/failed + 耗时 + 失败原因）。

## 数据模型

`notification_channel`（`NotificationChannelEntity`，IFullAudited 软删除）：

| 列 | 说明 |
|---|---|
| id | bigint identity |
| name | 渠道名称，`is_deleted=0` 内唯一（部分唯一索引） |
| type | NotificationChannelType（feishuWebhook=1/dingtalkWebhook=2/wecomWebhook=3/telegramBot=4/webhook=5/smtp=6/inSite=7），**创建后不可变更** |
| config | 渠道配置 JSON（≤4000，按类型结构化，见 NotificationChannelConfigs） |
| enabled | 停用后广播不投递、进行中的任务跳过 |
| description | 描述 |

`notification_log`（`NotificationLogEntity`，IFullAudited 软删除）：channel_id + channel_name 快照（渠道删除后可读）、channel_type、level、title（≤256）、content（≤4000 截断存档）、status（pending/success/failed）、error（≤1000）、duration_ms；索引 `(create_time desc)`、`(channel_id, create_time desc)`。

## 关键决策

1. **统一消息模型**：业务方只给 `Title/Content/Level/Source`；纯文本形态 `【MoAI】<级别图标> 标题\n正文\n来源`、Markdown 形态（企微）加粗标题+时间脚注，由 NotificationText 统一渲染，渠道差异全部封在发送器内。
2. **异步为默认，同步按需**：`NotifyAsync` 走 Hangfire（不阻塞业务流，失败自动重试 3 次，至少一次语义——超时场景可能重复推送）；`SendNowAsync` 供渠道测试等需要即时结果的场景；测试端点不落发送记录。
3. **发送器策略模式**：`INotificationChannelSender` 每类型一实现，Dispatcher 按 `channel.Type` 路由并把异常归一为 `(success, error, durationMs)`；新渠道扩枚举 + 加一个发送器即可。签名内聚在发送器内：飞书 `HMAC-SHA256(key="timestamp\nsecret", 空)`、钉钉 `HMAC-SHA256(key=secret, "timestamp\nsecret")` Base64 + URL 编码追加查询参数。
4. **复用既有底座不重复造轮**：SMTP 渠道 = 系统设置 SMTP + `IEmailSenderService`（未启用报 502）；站内推送 = `IMessageService.SendToUsersAsync`（userIds ∪ admin/root，root 走 setting 表 `root` 键识别）；通用 webhook 支持自定义请求头与请求体模板（`{title} {content} {level} {source} {time}` 占位符），无模板发默认 JSON，2xx 即成功。
5. **发送记录即审计**：每渠道每次投递一行，channel_name 做快照；管理页「发送记录」Tab 分页 + 按渠道/状态过滤。
6. **门禁**：Controller 层 `EnsureAdminAsync`（IsAdmin，403）；请求模型 `IModelValidator` 拦截格式错误；Handler 复核配置合法性（400/404），渠道类型不可变更。
7. **i18n**：错误消息以中文原文为 key，`src/MoAI/i18n/{zh-CN,en-US}.json` 同步登记；动态错误（外部渠道返回原文）不翻译原样回显。

## 已知问题

- 通用 webhook 无重试幂等键，至少一次语义下接收方需容忍重复；后续可加事件 id 头。
- 发送记录暂无自动清理策略（量大后加 Hangfire 归档）。
- 渠道配置 JSON 明文存储（管理员可见全量）；Webhook 密钥级掩码/信封加密待按需接入（参照 subsystem 的 reveal 机制）。
- 枚举成员命名需满足「camelCase(成员名) == JsonPropertyName」（钉钉成员为 `DingtalkWebhook`），否则 NSwag 契约与运行时反序列化分叉，前端 Kiota 发送的字符串会 400。
