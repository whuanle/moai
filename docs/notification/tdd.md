# 通知中心（Notification，NT）验证映射（TDD）

> 关联：[SDD](./sdd.md) ｜ [BDD](./bdd.md)（@NT-Sxx） ｜ [TDD](./tdd.md) ｜ [SOP](./sop.md) ｜ 证据：[local-dev/notification-e2e.mjs](../../local-dev/notification-e2e.mjs)

回归命令（需后端运行中；内置 webhook/SMTP 桩，零真实外部依赖）：

```bash
node local-dev/notification-e2e.mjs   # NT 106（应用机器人场景需后端以 MoAI:Feishu/DingTalk/WeixinWork:Endpoint 覆盖启动，见脚本文件头）
```

| 场景 | 验证物 | 结果（日期） |
|---|---|---|
| @NT-S1 | local-dev/notification-e2e.mjs#NT-01..04 | PASS 4/4（2026-11-02） |
| @NT-S2 | local-dev/notification-e2e.mjs#NT-05..09、NT-11、NT-114（NT-10 随收件人移出渠道配置退役，编号不复用） | PASS 7/7（2026-11-02） |
| @NT-S3 | local-dev/notification-e2e.mjs#NT-12..13 | PASS 2/2（2026-11-02） |
| @NT-S4 | local-dev/notification-e2e.mjs#NT-14..18 | PASS 5/5（2026-11-02） |
| @NT-S5 | local-dev/notification-e2e.mjs#NT-19..24、NT-48..55（桩内验签/协议载荷断言） | PASS 14/14（2026-11-02） |
| @NT-S6 | local-dev/notification-e2e.mjs#NT-25..28 | PASS 4/4（2026-11-02） |
| @NT-S21 | local-dev/notification-e2e.mjs#NT-29..30（系统 SMTP 关闭时渠道自带连接直达桩，试发携带收件人邮箱）+ local-dev/settings-smtp-e2e.mjs#SET-62/66/66d/67（渠道连接校验与免认证/AUTH 链路，SET-66d 断言信封 RCPT TO=试发指定收件人） | PASS（2026-11-02；旧 @NT-S7「联动系统设置」作废不复用） |
| @NT-S8 | local-dev/notification-e2e.mjs#NT-31 | PASS 1/1（2026-11-02） |
| @NT-S9 | local-dev/notification-e2e.mjs#NT-32..34（Hangfire 轮询终态；广播含 SMTP 渠道时附收件人邮箱） | PASS 3/3（2026-11-02） |
| @NT-S10 | local-dev/notification-e2e.mjs#NT-35..36 | PASS 2/2（2026-11-02） |
| @NT-S11 | local-dev/notification-e2e.mjs#NT-37..38 | PASS 2/2（2026-11-02） |
| @NT-S22 | local-dev/notification-e2e.mjs#NT-36（广播记录关联用户空串回显）；定向非空场景 @manual（当前无 REST 触达路径，业务方经 NotifyAsync 定向投递接入后补 e2e） | PASS（2026-11-02） |
| @NT-S12 | local-dev/notification-e2e.mjs#NT-39 | PASS 1/1（2026-11-02） |
| @NT-S13 | local-dev/notification-e2e.mjs#NT-40（/api/message/list 断言） | PASS 1/1（2026-11-02） |
| @NT-S14 | local-dev/notification-e2e.mjs#NT-41 | PASS 1/1（2026-11-02） |
| @NT-S15 | local-dev/notification-e2e.mjs#NT-42、NT-115..117（空标题 400 + 含 SMTP 缺邮箱收件人/含短信缺手机号/手机号格式非法 400） | PASS 4/4（2026-11-02） |
| @NT-S16 | local-dev/notification-e2e.mjs#NT-43..47（渠道多选/仅站内信/均不选 400） | PASS 5/5（2026-11-02） |
| @NT-S17 | local-dev/notification-e2e.mjs#NT-56..59（缺凭证 400 + 创建回显） | PASS 4/4（2026-11-02） |
| @NT-S18 | local-dev/notification-e2e.mjs#NT-60..63、NT-65（OAuth 桩换码 + 双用户三平台绑定） | PASS 5/5（2026-11-02） |
| @NT-S19 | local-dev/notification-e2e.mjs#NT-64、NT-66..68（未绑定失败回显 + 三平台试发协议断言） | PASS 4/4（2026-11-02） |
| @NT-S20 | local-dev/notification-e2e.mjs#NT-69、NT-70（广播绑定反查 + 跨企业换算失败 failed） | PASS 2/2（2026-11-02） |
| @NT-S23 | local-dev/notification-e2e.mjs#NT-80..89（门禁/重名/空正文/非法变量/更新重名与 404/列表回显） | PASS 12/12（2026-11-02） |
| @NT-S24 | local-dev/notification-e2e.mjs#NT-90..93（插值与花括号字面保留/缺变量 400/404/HTML 渲染） | PASS 4/4（2026-11-02） |
| @NT-S25 | local-dev/notification-e2e.mjs#NT-113（删除后列表移除 + 历史记录保留） | PASS 1/1（2026-11-02） |
| @NT-S31 | local-dev/notification-e2e.mjs#NT-89b..f（内置模板 isBuiltIn 回显/删除 400/编辑生效并还原） | PASS 5/5（2026-11-02） |
| @NT-S26 | local-dev/notification-e2e.mjs#NT-94、NT-95b、NT-95c（缺签名/缺 SdkAppId 400 + 创建回显；NT-95 随手机号移出渠道配置退役，编号不复用） | PASS 3/3（2026-11-02） |
| @NT-S27 | local-dev/notification-e2e.mjs#NT-96..98、NT-118（无模板直发失败/未映射阿里 Code/未映射腾讯 Id/模板试发缺手机号失败提示动态收件人） | PASS 4/4（2026-11-02） |
| @NT-S28 | local-dev/notification-e2e.mjs#NT-99..105（试发附收件人手机号；POP 查询串断言/业务错误码归一 + TC3 头与请求体断言/参数顺序/SendStatusSet 与 Error 归一） | PASS 10/10（2026-11-02） |
| @NT-S29 | local-dev/notification-e2e.mjs#NT-106..107（SMTP 桩 MIME base64/QP 解码断言 text/html + webhook 降级去标签） | PASS 2/2（2026-11-02） |
| @NT-S30 | local-dev/notification-e2e.mjs#NT-108..112（标题正文可空入队/缺变量 400/模板不存在 404/渲染文本落记录与站内信） | PASS 5/5（2026-11-02） |
| @NT-S32 | local-dev/notification-e2e.mjs#NT-119（广播 SMTP 渠道记录快照实际收件人邮箱、webhook 固定地址渠道留空） | PASS 1/1（2026-11-02） |

## 自检记录（2026-11-02）

- `dotnet build src/MoAI/MoAI.csproj`：0 错误。
- `node local-dev/notification-e2e.mjs`：**PASS 106 / FAIL 0**（应用机器人段需端点覆盖启动，未覆盖自动 SKIP，与本次改动无关）。
- `cd ui && npm run typecheck`：0 错误；`npm run lint`：0 错误（15 条存量警告，无本模块新增）；`npm run test`：712/712。
- E2E 可重复执行：渠道与模板按运行标识命名、结束自清理；SMTP 系统绑定两键（SMTP_ENABLED/SMTP_CHANNEL_ID）开头快照、结束恢复。

### 第七轮（收件人移出渠道配置、发送时动态指定，97 → 106 用例）

- SMTP「收件人/邮件主题前缀」与短信「收件人手机号列表」移出渠道配置（`SmtpChannelConfig`/`AliyunSmsConfig`/`TencentSmsConfig`
  删字段，存量配置 JSON 中的 recipients/subjectPrefix 键反序列化自动忽略，渠道创建/更新不再校验收件人）；
  `NotificationMessage` 新增 `EmailRecipients`（仅 SMTP 生效）/`PhoneNumbers`（仅短信生效），`NotificationTemplateApplier` 渲染时透传。
- 发送时动态收件人（@NT-S2/@NT-S15/@NT-S21/@NT-S26..S28）：广播 `BroadcastNotificationCommand` 加 `emailRecipients`/`phoneNumbers`
  （静态校验格式 + Handler 按所选渠道类型校验必填，400）；渠道试发与模板试发加 `recipients`（按渠道类型分发到
  EmailRecipients/PhoneNumbers）；SMTP 主题=message.Title 不再叠加前缀，缺收件人发送器抛「未指定收件人邮箱/手机号」。
- 发送记录留痕（@NT-S32）：`notification_log` 新增 `recipients varchar(1000)` 快照列（SMTP 记邮箱、短信记手机号、逗号分隔超长截断、
  固定地址渠道留空），CreateLog 填充，查询 DTO 回显 `recipients`；存量库由幂等 DDL 补列（asserts/notification.sql 同步）。
- e2e：新增 NT-114..119；退役不复用 NT-10（smtp 无效邮箱 400）/NT-95（渠道配置无效手机号 400）；既有广播场景带 emailRecipients、
  短信试发带 recipients。配套 settings-smtp（SET-66d 信封 RCPT TO=试发指定收件人）/auth-email-code/auth-2fa（验证码收件人本就
  动态，仅清理废弃字段）/loginsafety 四脚本同步全绿（SET 36 / AUTH 39 / AUTH-2FA 54 / LS 39）。
- 回归：全量 **NT 106/106 全绿**（应用机器人段仍需端点覆盖启动，否则自动跳过）。

### 第六轮（通知模板 + 阿里云/腾讯云短信，55 → 97 用例）

- 新增 `notification_template` 表与模板 CRUD/测试端点（@NT-S23..S25）：模板 = 名称 + 主题（邮件用）+ 正文 + 内容类型（text/html）+
  变量定义 JSON + 阿里云模板 Code/腾讯云模板 Id 映射；`NotificationTemplateApplier` 在领域服务入口（NotifyAsync/SendNowAsync）与
  广播 Handler 处渲染一次并清空 TemplateId（幂等，Hangfire 队列中的消息不依赖模板存续）；渲染器
  `NotificationTemplateRenderer` 手写 `{{key}}` 双花括号白名单插值（2026-11-02 从单花括号切换，与云邮件平台提审形态一致；白名单外花括号按字面保留）。
- 新增 aliyunSms=19/tencentSms=20 渠道（@NT-S26..S28）：短信渠道配置收件人手机号列表（与 SMTP Recipients 同模式，忽略 TargetUserIds），
  **必须经模板发送**（无模板/未映射平台模板 → 该渠道 failed 不阻塞其它渠道）；阿里云 Dysmsapi SendSms 走 POP RPC 签名 V1（HMAC-SHA1），
  腾讯云走 TC3-HMAC-SHA256，TemplateParamSet 按模板正文占位符出现顺序（applier 预提取 SmsTemplateParamNames）。
- HTML 模板（@NT-S29）：SMTP 渠道按 ContentType 发 text/html（模板自带样式不再叠加【MoAI】前缀）；webhook/推送/站内等渠道经
  `NotificationText.ContentText` 统一降级去标签。
- 广播带模板（@NT-S30）：`BroadcastNotificationCommand` 加 templateId/templateVariables，使用模板时标题正文可空；站内信路径同样渲染并降级。
- 内置通知模板（@NT-S31）：`notification_template` 加 `code` 列（非空即内置，唯一部分索引），`NotificationTemplateSeed` 种子
  注册/登录/找回密码三条验证码邮件模板（HTML），删除 Handler 拦内置 400、列表回显 `isBuiltIn`；新增
  `INotificationTemplateRenderService`（Shared 接口 + Core 实现，复用 Applier）供 auth 按 id/code 渲染验证码邮件，见
  [settings/sdd 决策 15](../settings/sdd.md)。
- e2e 坑三处：① 短信桩 endpoint 必须带路径（裸 STUB 打到 `/` 命中默认响应）；② applier 渲染后消息曾丢 TemplateVariables 致短信参数
  全空——渲染产物需透传变量值表；③ MailKit 对 text/html 正文做 base64 编码，桩侧断言需解码（decodeMailBody 支持 base64 与
  quoted-printable，注意跳过头部与正文间空行）。
- 回归：全量 **NT 97/97 全绿**（模板/短信段 NT-80..113 并入主脚本；应用机器人段仍需端点覆盖启动，否则自动跳过）。

### 第五轮（SMTP 连接配置收敛，@NT-S7 作废 → @NT-S21）

- SMTP 渠道改为**自带完整连接配置**（host/端口/加密/账号/发件人/广播收件人，`SmtpChannelConfig` 扩展），
  不再复用系统设置；系统设置「邮箱验证」仅 `SMTP_ENABLED`+`SMTP_CHANNEL_ID` 绑定渠道，系统邮件
  （验证码/安全告警）由本模块 `SystemEmailSenderService`（IEmailSenderService 实现，接口留 Settings.Shared）
  按 `SmtpMailTransport` 共用传输发送——settings 模块删除 MailKit 发送与 `POST /settings/email/test` 端点。
- 校验：渠道创建时校验 host 必填/端口 1~65535/加密枚举/发件人邮箱（@NT-S21 + [@SET-S61](../settings/bdd.md#set-s61)）。
- 回归：notification-e2e NT-29b 断言「系统邮箱验证关闭时渠道发信仍成功」；settings-smtp/auth-email-code/auth-2fa/
  loginsafety 四脚本同步改走渠道绑定语义后全绿（SET 32 / AUTH 34 / AUTH-2FA 54 / LS 38）。

### 第一轮（初版 42 用例）

- 首轮 30/12，修复 4 类问题后全绿：
  ① 响应 long/枚举为字符串，断言需按字符串比较；② Update 命令 Id 由路由晚于模型验证赋值，校验规则移入 Handler；
  ③ 枚举成员 `DingTalkWebhook` 改名 `DingtalkWebhook` 对齐 NSwag 契约（camelCase(成员名)==JsonPropertyName）；
  ④ 环境坑：遗留的 loginsafety e2e 后端（旧构建，端口 5213）与新后端共享同一 Redis Hangfire 存储，抢占通知任务致 pending——
  终止遗留实例后恢复，见 SOP 排障表第 1 条。

### 第二轮（UI 反馈迭代，42 → 47 用例）

- 用户反馈三处：① 渠道类型下拉去掉「站内推送」（站内信无需配置，改由广播勾选）；② 渠道搜索框与查询按钮贴合（裸 Input 未包 Form.Item，
  QueryBar inline 布局间距来自 Form.Item，已包裹并改为 form 取值）；③ 广播弹窗支持目标组合——渠道多选（channelIds）与
  「发送站内信」（sendInSite，直发全部启用用户，无需渠道配置）至少选其一，可组合（BroadcastNotificationCommand 校验
  `AllChannels || SendInSite || ChannelIds.Count > 0`，否则 400；旧「空=全部」语义由显式 allChannels 承担）。
- 回归：NT 47/47 全绿（含新增 @NT-S16 五用例）。

### 第三轮（列宽与渠道扩展，47 → 55 用例）

- 用户反馈两处：① 操作列三个按钮换行——操作列宽 170 → 220；② 渠道类型对标 Grafana 扩容——新增 Slack/Discord/Microsoft Teams/
  Gotify/ntfy/Bark/Server酱/PushPlus 八类（均纯 HTTP 协议；Server酱/PushPlus/BaseUrl 类均支持自建地址），共 14 类外部渠道 + 站内推送。
- e2e 坑：桩 Bark 路径误写为 /bark/push（实际请求 /push），桩命中 default 分支返回无 code 响应致失败，修正后全绿。
- 回归：NT 55/55 全绿（NT-48..55 八类渠道试发协议断言 + 广播 16 渠道）。

### 第四轮（应用机器人三渠道，55 → 70 用例）

- 新增 feishuAppBot/dingtalkAppBot/wecomAppBot（@NT-S17..S20）：App 凭证独立于第三方登录连接，按 user_oauth_connection 绑定反查
  平台标识（飞书 union_id 优先/open_id 兜底、钉钉 unionId→getbyunionid 换算 userid、企微 userid 直投）；试发定向当前管理员
  （TestNotificationChannelCommand 继承 IUserIdContext → NotificationMessage.TargetUserIds）。
- 配套：Infra 三平台登录客户端端点可覆盖（MoAI:Feishu/DingTalk/WeixinWork:Endpoint，与 BoCha/Jina 等先例一致），E2E 桩得以覆盖登录换码；
  未覆盖时脚本探测后自动跳过 NT-56..70。
- e2e 坑两处：① `oauth_bind_account` 既有校验为「同一连接仅允许一个用户绑定」（按 providerId 判重而非 sub），admin/member 各需一套
  连接；② 飞书 im/v1 的 content 是 JSON 字符串套 JSON，非 ASCII 转义为 \uXXXX，断言需先解析内层 JSON 再匹配中文。
- 回归：应用机器人段 NT-56..70 探针 16/16 全绿后并入主脚本，全量 **NT 72/72 全绿**（2026-11-02，后端需带 `MoAI:Feishu/DingTalk/WeixinWork:Endpoint` 端点覆盖启动）。
- 顺带适配：SMTP 渠道切换为自带连接配置后，NT-29/30 与创建参数同步改为直连 SMTP 桩语义（不再依赖系统设置启用）。
