# 通知中心（Notification，NT）设计规格（SDD）

> 关联：[SDD](./sdd.md) ｜ [BDD](./bdd.md)（@NT-Sxx） ｜ [TDD](./tdd.md) ｜ [SOP](./sop.md) ｜ 上游：[../message/sdd.md](../message/sdd.md)（站内信）、[../settings/sdd.md](../settings/sdd.md)（邮箱验证绑定 SMTP 渠道） ｜ 证据：[local-dev/notification-e2e.mjs](../../local-dev/notification-e2e.mjs)
> 规范：[../DOC-STANDARD.md](../DOC-STANDARD.md)。行为场景见 BDD，本文不重复。

## 目标

统一承接平台各类通知出口（飞书/钉钉/企业微信机器人 webhook、飞书/钉钉/企业微信应用机器人（按第三方账号绑定反查个人推送）、Telegram Bot、通用 webhook、SMTP 邮件、站内推送、阿里云/腾讯云短信），**业务方只调统一领域服务接口，不感知任何外部接口格式**。解决「每个需要通知的场景各自配一套渠道」的碎片化：渠道集中配置、集中测试、集中留痕（发送记录），子系统后续接入复用同一模块。通知模板支持「文本 + 变量定义」的自定义插值渲染，邮件/短信模板可复制到外部平台提审对照，业务方发送时可不使用模板直发，也可指定模板 + 变量值由领域服务渲染。

## 组件（源码树）

```
src/notification/
  MoAI.Notification.Shared/      NotificationSharedModule
    Models/                      NotificationMessage（统一消息：Title/Content/Level/Source/TargetUserIds 定向 + EmailRecipients/PhoneNumbers 发送时动态收件人 + TemplateId/TemplateVariables 模板渲染入参 + 渲染产物 Sms 短信映射字段）、
                                 NotificationSendOptions（ChannelIds 定向）、NotificationChannelSendResult、
                                 NotificationChannelConfigs（二十类渠道配置 POCO + JSON 解析/校验 + CheckEmailRecipients/CheckPhoneRecipients 动态收件人格式校验）、
                                 NotificationTemplateVariable(Parser)（模板变量定义 JSON 解析/校验/缺失值检查）
    Services/                    INotificationService（领域服务唯一入口）
    Commands/Queries/            渠道 CRUD/测试/广播（渠道多选+站内信组合，广播支持 templateId+变量）+
                                 模板 CRUD/测试（template/test：渲染预览或按渠道试发）+ 渠道/模板列表/发送记录分页（+Responses）
  MoAI.Notification.Core/        NotificationCoreModule（注册 HttpClient + 20 个发送器）
    Services/                    NotificationService（[InjectOnScoped]，NotifyAsync=Hangfire 异步 / SendNowAsync=同步，入口经模板应用器渲染）、
                                 NotificationTemplateApplier（模板渲染管线：查模板→校验变量完整性→插值渲染→填充短信平台映射与参数顺序，渲染一次并清空 TemplateId，幂等）、
                                 NotificationTemplateRenderer（{{key}} 双花括号白名单插值 + 占位符顺序提取 + HTML 去标签降级）、
                                 NotificationChannelDispatcher（按类型路由发送器、计时、异常归一）、
                                 NotificationSendJob（Hangfire 一次性任务，[AutomaticRetry(3)]）、
                                 NotificationPlatformUserResolver（站内用户→平台标识反查，应用机器人渠道用）
    Senders/                     INotificationChannelSender + 20 实现：Feishu/DingTalk/Wecom webhook、
                                 Feishu/DingTalk/Wecom AppBot（按绑定反查个人单聊投递）、TelegramBot、
                                 Slack/Discord/MicrosoftTeams、GenericWebhook（自定义头+模板）、SmtpChannel（自带连接配置经 SmtpMailTransport，收件人=message.EmailRecipients、主题=message.Title，HTML 模板发 text/html）、
                                 InSiteChannel（复用 IMessageService）、Gotify/ntfy/Bark/ServerChan/PushPlus、
                                 AliyunSms（Dysmsapi SendSms，POP RPC 签名 V1/HMAC-SHA1）、TencentSms（SendSms，TC3-HMAC-SHA256，收件人均=message.PhoneNumbers）；
                                 SmtpMailTransport（MailKit 传输，广播与系统邮件共用）+ SystemEmailSenderService（IEmailSenderService 实现：按「邮箱验证」绑定渠道发送）；
                                 NotificationText（纯文本/Markdown 渲染 + ContentText 按 ContentType 降级）、SenderHttp
    Handlers/Queries/            渠道六个 + 模板五个 CQRS Handler
  MoAI.Notification.Api/         NotificationApiModule
    Controllers/                 NotificationController（/api/notification/*，全部管理员门禁：渠道/模板 CRUD+测试、广播、发送记录）
src/database/.../Entities/Notification{Channel,Log,Template}Entity.cs + Database.Postgres/Data/*Configuration.cs（幂等 DDL 见 asserts/notification.sql）
ui/src/pages/admin/NotificationCenter.tsx + ui/src/api/notification.ts（渠道管理/通知模板/发送记录三 Tab 管理页）
```

调用链：业务模块 → `INotificationService.NotifyAsync`（携带 TemplateId 时先经 `NotificationTemplateApplier` 渲染）→ 解析启用渠道（可 ChannelIds 定向）→ 每渠道落 `notification_log`（Pending）→ Hangfire Enqueue → `NotificationSendJob` → `NotificationChannelDispatcher` → 渠道发送器 → 外部接口；终态回写 log（success/failed + 耗时 + 失败原因）。

## 数据模型

`notification_channel`（`NotificationChannelEntity`，IFullAudited 软删除）：

| 列 | 说明 |
|---|---|
| id | bigint identity |
| name | 渠道名称，`is_deleted=0` 内唯一（部分唯一索引） |
| type | NotificationChannelType（feishuWebhook=1/dingtalkWebhook=2/wecomWebhook=3/telegramBot=4/webhook=5/smtp=6/inSite=7/slack=8/discord=9/microsoftTeams=10/gotify=11/ntfy=12/bark=13/serverChan=14/pushPlus=15/feishuAppBot=16/dingtalkAppBot=17/wecomAppBot=18/aliyunSms=19/tencentSms=20），**创建后不可变更** |
| config | 渠道配置 JSON（≤4000，按类型结构化，见 NotificationChannelConfigs；SMTP 收件人/主题前缀与短信收件人手机号已移出配置，改为发送时动态指定，存量配置 JSON 中的 recipients/subjectPrefix 键反序列化自动忽略） |
| enabled | 停用后广播不投递、进行中的任务跳过 |
| description | 描述 |

`notification_log`（`NotificationLogEntity`，IFullAudited 软删除）：channel_id + channel_name 快照（渠道删除后可读）、channel_type、level、title（≤256）、content（≤4000 截断存档）、status（pending/success/failed）、error（≤1000）、duration_ms、target_user_ids（≤1000，关联用户 id 快照：`NotificationMessage.TargetUserIds` 逗号分隔存档，定向个人推送时记录收件人，广播/全员为空串；列表接口按快照 id 回填 `targetUserNames`，已删除用户跳过；存量库由幂等 DDL 补列）、recipients（≤1000，实际收件人快照：SMTP 记邮箱、短信记手机号，逗号分隔超长截断，固定地址渠道留空；CreateLog 从消息动态收件人填充，列表接口回显 `recipients`；存量库由幂等 DDL 补列）；索引 `(create_time desc)`、`(channel_id, create_time desc)`。

`notification_template`（`NotificationTemplateEntity`，IFullAudited 软删除）：

| 列 | 说明 |
|---|---|
| id | bigint identity |
| code | 内置模板标识（≤64，非空即内置模板；`is_deleted=0 AND code<>''` 唯一部分索引；种子内置注册/登录/找回密码验证码邮件三条，业务按 code 回退解析） |
| name | 模板名称，`is_deleted=0` 内唯一（部分唯一索引） |
| title | 通知主题模板（≤256，邮件主题用可含 {{变量}} 双花括号占位符，短信无主题） |
| content | 通知正文模板（≤10000，纯文本或 HTML，含 {{变量}} 双花括号占位符，与云邮件平台提审形态一致） |
| content_type | NotificationContentType（text=0/html=1） |
| variables | 变量定义 JSON（≤4000：`[{"name","description","example"}]`，name 字母开头仅字母/数字/下划线且不重复） |
| aliyun_template_code | 阿里云短信模板 Code（≤128，平台审核通过后回填，空=不用于阿里云短信） |
| tencent_template_id | 腾讯云短信模板 Id（≤128，同上） |
| description | 描述 |

**内置模板种子**：`NotificationTemplateSeed`（HasData，新库 EnsureCreated 自动写入；存量库补种 `psql -f asserts/notification-template.sql`，省略 id 走 identity 按 code 判存在）——`register_email_code`/`login_email_code`/`reset_email_code` 三条验证码邮件模板（HTML，ContentType=html，变量 code/siteName/expireMinutes），**不可删除**（删除 Handler 拦 400）、**可编辑内容**；列表响应带 `isBuiltIn`。系统设置「邮箱验证」绑定模板 id 留空时，验证码邮件按用途按 code 自动回退（渲染出口 `INotificationTemplateRenderService`，供 auth 模块复用 Applier 语义），见 [settings/sdd 决策 15](../settings/sdd.md)。

## 关键决策

1. **统一消息模型**：业务方只给 `Title/Content/Level/Source`；纯文本形态 `【MoAI】<级别图标> 标题\n正文\n来源`、Markdown 形态（企微）加粗标题+时间脚注，由 NotificationText 统一渲染，渠道差异全部封在发送器内。
2. **异步为默认，同步按需**：`NotifyAsync` 走 Hangfire（不阻塞业务流，失败自动重试 3 次，至少一次语义——超时场景可能重复推送）；`SendNowAsync` 供渠道测试等需要即时结果的场景；测试端点不落发送记录。
3. **广播目标组合**：`BroadcastNotificationCommand` 的投递目标 = 渠道（`channelIds` 指定集合 或 `allChannels` 全部启用）与「发送站内信」（`sendInSite`，经 `IMessageService.SendToAllUsersAsync` 直发全部启用用户，**无需渠道配置**）的组合，至少选其一（校验 400）；站内信选项不落 notification_log（消息本身即记录，在消息中心可查）。**站内推送不作为可创建渠道类型**（前端下拉隐藏；枚举与发送器保留以兼容历史数据与定向调用）。
4. **发送器策略模式**：`INotificationChannelSender` 每类型一实现，Dispatcher 按 `channel.Type` 路由并把异常归一为 `(success, error, durationMs)`；新渠道扩枚举 + 加一个发送器即可。签名内聚在发送器内：飞书 `HMAC-SHA256(key="timestamp\nsecret", 空)`、钉钉 `HMAC-SHA256(key=secret, "timestamp\nsecret")` Base64 + URL 编码追加查询参数。
5. **复用既有底座不重复造轮**：SMTP 渠道**自带完整连接配置**（host/端口/加密/账号/发件人，经 `SmtpMailTransport` MailKit 直发；2026-11-02 起不再复用系统设置——系统设置「邮箱验证」仅绑定渠道 id，系统邮件由 `SystemEmailSenderService` 按 `SMTP_ENABLED`+`SMTP_CHANNEL_ID` 定位渠道后走同一传输，见 [settings/sdd 决策 14](../settings/sdd.md)）；**收件人与邮件主题前缀已移出渠道配置**——收件人由发送时动态指定（`NotificationMessage.EmailRecipients`，广播/试发传入，缺失该渠道投递失败并留痕），邮件主题=message.Title 不再叠加固定前缀（系统验证码邮件本就动态收件人，不受影响）；站内推送渠道 = `IMessageService.SendToUsersAsync`（userIds ∪ admin/root，root 走 setting 表 `root` 键识别）；通用 webhook 支持自定义请求头与请求体模板（`{title} {content} {level} {source} {time}` 占位符），无模板发默认 JSON，2xx 即成功。
6. **发送记录即审计**：每渠道每次投递一行，channel_name 做快照；管理页「发送记录」Tab 分页 + 按渠道/状态过滤，含创建/更新审计列、「关联用户」列（`target_user_ids` 快照回显用户名）与「收件人」列（`recipients` 快照回显实际收件人邮箱/手机号；渠道测试同步直发不落记录，定向快照随业务方 `NotifyAsync(TargetUserIds)` 落库）。
7. **门禁**：Controller 层 `EnsureAdminAsync`（IsAdmin，403）；请求模型 `IModelValidator` 拦截格式错误；Handler 复核配置合法性（400/404），渠道类型不可变更。
8. **i18n**：错误消息以中文原文为 key，`src/MoAI/i18n/{zh-CN,en-US}.json` 同步登记；动态错误（外部渠道返回原文）不翻译原样回显。
9. **应用机器人渠道按绑定反查个人**（feishuAppBot/dingtalkAppBot/wecomAppBot）：IM 渠道分两族——**webhook 族「自定义机器人」**（飞书自定义机器人/钉钉自定义机器人/企微群机器人，在群聊内创建，配置 Webhook 地址+可选签名密钥，只能推群聊）与**应用族「应用机器人」**（App 凭证，按用户绑定反查可对个人推送），前端类型命名与官方术语对齐以防混淆。App 凭证在渠道配置内独立填写（**通知应用 ≠ 第三方登录应用**），
   投递目标由 `NotificationPlatformUserResolver` 反查 `oauth_connection`（按 provider）∪ `user_oauth_connection`（启用用户、同用户取最新绑定）得到
   平台侧标识；跨应用可用性由平台语义保证——飞书优先 union_id（同主体跨应用一致，`receive_id_type=union_id`，缺失退回 open_id）、
   钉钉绑定 unionId 经 `topapi/user/getbyunionid` 换算通知应用所属企业内 userid（跨企业换算失败跳过，全部失败则渠道 failed）、
   企业微信 userid 企业内跨应用一致直投。`NotificationMessage.TargetUserIds` 支持定向（渠道测试 = `IUserIdContext` 定向当前管理员本人，
   广播为空 = 全部绑定用户）；零绑定/零换算渠道 failed 带原因。配套 Infra 三平台登录客户端端点可配置覆盖
   （`MoAI:Feishu/DingTalk/WeixinWork:Endpoint`，先例同 BoCha/Jina），E2E 桩由此覆盖登录换码。
10. **模板渲染管线（渲染一次，幂等）**：`NotificationMessage.TemplateId/TemplateVariables` 由 `NotificationTemplateApplier` 在领域服务入口
   （NotifyAsync/SendNowAsync）与广播 Handler 处解析——查模板（404）→ 校验变量值覆盖全部定义（400 列缺失名）→ `{{key}}` 双花括号白名单插值渲染
   标题/正文（未定义占位符按字面保留，语义对齐 variable 模块 TeamVariableTemplate，各模块独立实现不跨模块引用）→ 构造渲染后消息并
   **清空 TemplateId**（重复应用为无操作；Hangfire 队列中的消息不依赖模板存续，模板删除不影响已入队与已发送内容）。渲染产物携带
   ContentType 与短信平台映射（SmsAliyunTemplateCode/SmsTencentTemplateId/SmsTemplateParamNames 占位符顺序），多渠道投递互不干扰；
   动态收件人字段（EmailRecipients/PhoneNumbers）渲染时原样透传。
   模板管理端点 `template/test`：不传 channelId 仅渲染预览，传了则同步试发并落发送记录。
11. **短信渠道必须经模板**：阿里云/腾讯云短信接口只接受平台侧已审核模板（Code/Id）+ 参数，不允许任意文本——收件人手机号发送时动态指定
   （`NotificationMessage.PhoneNumbers`，广播/试发传入，缺失该渠道 failed，忽略 TargetUserIds）；无模板直发或模板未映射对应平台 → 该渠道 failed 带原因，不阻塞其它渠道。
   阿里云走 Dysmsapi SendSms（RPC 签名 V1/HMAC-SHA1，TemplateParam=变量值 JSON 按名）；腾讯云走 TC3-HMAC-SHA256 签名
   （TemplateParamSet 为位置数组，按模板正文占位符出现顺序取值）；平台错误码/状态与 HTTP 非 2xx 均归一为 failed 原因。两家均支持
   Endpoint 覆盖（自建网关/E2E 桩）。模板正文即外部平台提审内容的对照源（在线平台模板审核场景复制即用）。
12. **HTML 模板分渠道投递**：`ContentType=html` 时 SMTP 渠道发 `text/html`（模板自带样式，不再叠加【MoAI】前缀与来源脚注）；其余渠道经
   `NotificationText.ContentText` 统一降级为去标签纯文本（含广播站内信路径）。

## 已知问题

- 通用 webhook 无重试幂等键，至少一次语义下接收方需容忍重复；后续可加事件 id 头。
- 发送记录暂无自动清理策略（量大后加 Hangfire 归档）。
- 渠道配置 JSON 明文存储（管理员可见全量）；Webhook 密钥级掩码/信封加密待按需接入（参照 subsystem 的 reveal 机制）。
- 枚举成员命名需满足「camelCase(成员名) == JsonPropertyName」（钉钉成员为 `DingtalkWebhook`/`DingtalkAppBot`），否则 NSwag 契约与运行时反序列化分叉，前端 Kiota 发送的字符串会 400。
- 应用机器人渠道投递能力依赖用户主动绑定第三方账号（个人中心），未绑定用户无法触达；钉钉跨企业（通知应用与登录应用不同企业）unionId 无法换算，相关绑定跳过。
- `oauth_bind_account` 既有校验为「同一 OAuth 连接仅允许一个用户绑定」（按 providerId 判重，见 account 模块），多用户测试需各自一套连接；E2E 已按此规避。
- 发送记录关联用户快照记录的是**消息声明的定向收件人**（`TargetUserIds`），非发送时反查的实际收件人；当前落记录路径（广播/风控告警）均为全员语义故为空串。SMTP/短信的实际收件人（发送时动态指定）已由 `recipients` 快照列留痕；应用机器人广播的实际收件人（按绑定反查）暂不回写记录，需要时由发送器回写（待按需）。
- 短信渠道内容完全由平台侧已审核模板决定，MoAI 侧渲染文本仅用于发送记录留痕；站内用户表无手机号字段，短信无法按站内用户定向，收件人手机号只能由发送方动态指定（广播/试发由管理员填写）。
