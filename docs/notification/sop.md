# 通知中心（Notification，NT）运维手册（SOP）

> 关联：[SDD](./sdd.md) ｜ [BDD](./bdd.md)（@NT-Sxx） ｜ [TDD](./tdd.md) ｜ [SOP](./sop.md)

## 日常操作

1. **新增渠道**：管理侧边栏「通知中心」→ 渠道管理 → 新建渠道 → 选类型填配置 → 「保存前试发」验证连通 → 保存。各类渠道配置要点见编辑表单内联提示（SDD 数据模型节）。
2. **接入新通知场景（业务方）**：引用 `MoAI.Notification.Shared`，注入 `INotificationService`：
   - `NotifyAsync(new NotificationMessage { Title, Content, Level, Source })` —— 投递全部启用渠道，Hangfire 异步、失败重试 3 次；
   - `NotifyAsync(msg, new NotificationSendOptions { ChannelIds = [...] })` —— 定向渠道；
   - `SendNowAsync(...)` —— 同步拿每渠道结果（谨慎用于请求线程，渠道超时 15s）。
3. **验证渠道连通**：列表行「测试」按 id 试发；「发送广播」验证全部启用渠道，结果看发送记录 Tab（可按渠道/状态过滤）。
4. **新渠道类型接入**：扩 `NotificationChannelType` 枚举（**成员名 camelCase 必须等于 JsonPropertyName**）→ `NotificationChannelConfigs` 加配置校验分支 → Core 增 `INotificationChannelSender` 实现并在 `NotificationCoreModule` 注册 → 前端表单加对应字段块。
5. **新建通知模板**：通知中心 → 通知模板 Tab → 新建模板 → 填名称/主题（邮件用）/正文（{{变量}} 双花括号占位符，与云邮件平台提审形态一致）/变量定义（变量名+说明+示例值）→ HTML 邮件模板选 HTML 类型（可从在线邮箱平台复制）→ 阿里云/腾讯云平台审核通过后回填模板 Code/Id → 「测试」渲染预览并可选渠道试发。
6. **业务方用模板发送**：`NotificationMessage` 携带 `TemplateId + TemplateVariables`（正文用 {{变量名}} 双花括号占位，变量值需覆盖模板全部定义，缺失 400）；不携带即直发（原行为不变）；短信渠道必须经模板且模板已映射对应平台模板 Code/Id。
7. **验证码邮件模板定制**：平台内置注册/登录/找回密码三条验证码邮件模板（列表带「内置」标识，不可删除可编辑）——改品牌色/文案直接编辑内置模板即可；或新建模板后在系统设置「邮箱验证」绑定对应用途的模板 id，解绑回退内置模板。
8. **短信渠道接入**：阿里云控制台创建 AccessKey 与签名/模板，模板正文从本平台模板复制提审；腾讯云同理（需 SdkAppId）；渠道配置填凭证+签名+收件人手机号列表，Endpoint 留空走官方地址。
7. **短信渠道接入**：阿里云控制台创建 AccessKey 与签名/模板，模板正文从本平台模板复制提审；腾讯云同理（需 SdkAppId）；渠道配置填凭证+签名+收件人手机号列表，Endpoint 留空走官方地址。

## 排障表

| 现象 | 原因 | 处置 |
|---|---|---|
| 新建渠道想填 App ID/密钥，表单却出现 Webhook 地址 | 选到了 webhook 族「自定义机器人」（飞书自定义机器人/钉钉自定义机器人/企微群机器人，在群聊内创建、只能推群聊，这是三家官方均存在的功能） | 要按用户对个人推送，改选「飞书/钉钉/企业微信应用机器人」类型（App 凭证 + AgentId）；推群聊则按表单提示在群设置里添加自定义机器人取 Webhook 地址 |
| 发送记录长期停在「待发送」，本机日志无任何投递/异常 | 存在同一 Redis（Hangfire 前缀 `moai:hangfire`）上的**其他 MoAI 实例**（如 e2e 遗留的旧构建进程）抢占任务且无法激活任务类 | 找出并终止多余实例（`SMEMBERS moai:hangfireservers` 可见全部注册实例与其 PID），重发通知 |
| SMTP 渠道测试发信失败 | 渠道自身连接配置问题（host/端口/加密/账号/发件人） | 编辑渠道核对连接配置后重试「测试」；系统设置「邮箱验证」只影响绑定后的系统邮件，不影响渠道广播发信 |
| 测试返回「飞书/钉钉机器人返回错误：sign …」 | 签名密钥与机器人安全设置不匹配 | 飞书填「签名密钥」原文；钉钉填 SEC 开头密钥；未开启校验则留空 |
| Telegram 测试 `Unauthorized` | Bot token 错误 | 向 BotFather 重取 token；网络不可达时 baseUrl 指向镜像 |
| 通用 webhook 返回 HTTP 4xx/5xx | 接收方校验失败 | 检查自定义请求头与请求体模板；无模板时默认 JSON 结构为 `{title,content,level,source,time}` |
| 站内推送收不到 | 渠道未配置收件人（userIds 空且未开推送管理员） | 编辑渠道补收件人或开启「推送全部管理员」 |
| 短信渠道发送记录 failed 提示「需使用通知模板/需配置模板 Code」 | 阿里云/腾讯云短信接口只接受平台侧已审核模板，不允许任意文本 | 模板上回填平台审核通过的阿里云模板 Code / 腾讯云模板 Id 后再发送；模板管理页「测试」可验证 |
| 短信试发 failed 提示平台错误码（如 isv.* / LimitExceeded.* / AuthFailure.*） | 阿里云/腾讯云平台侧拒绝（签名不匹配、流控、号码格式等） | 按错误码排查平台侧配置；确认渠道签名名与平台一致、手机号格式正确、模板状态为已通过 |
| 渲染预览返回 400 列出变量名 | 提供的变量值未覆盖模板全部变量定义 | 按模板变量定义补齐变量值（管理页测试弹窗会按定义生成输入框） |
| 重试后成功但对方收到多条 | Hangfire 至少一次语义（超时/响应解析失败会重试） | 接收方按内容幂等处理；后续可加事件 id 头 |

## 验收流程

1. `node local-dev/notification-e2e.mjs` 全绿（97 用例，映射见 [tdd.md](./tdd.md)）。
2. 浏览器走查（@manual）：管理员可见侧边栏「通知中心」；渠道管理 Tab 新建/试发/编辑/删除；通知模板 Tab 新建（含变量定义与平台模板映射）/渲染测试/编辑/删除；发送记录 Tab 过滤分页与状态/级别展示；广播弹窗可用模板+变量发送；非管理员访问 `/admin/notifications` 跳回 /apps。
