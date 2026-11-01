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

## 排障表

| 现象 | 原因 | 处置 |
|---|---|---|
| 发送记录长期停在「待发送」，本机日志无任何投递/异常 | 存在同一 Redis（Hangfire 前缀 `moai:hangfire`）上的**其他 MoAI 实例**（如 e2e 遗留的旧构建进程）抢占任务且无法激活任务类 | 找出并终止多余实例（`SMEMBERS moai:hangfireservers` 可见全部注册实例与其 PID），重发通知 |
| 渠道测试返回「SMTP 邮件服务未启用」 | 系统设置未启用/未配置 SMTP | 系统设置 → 邮件服务配置并启用（SMTP 渠道复用该配置） |
| 测试返回「飞书/钉钉机器人返回错误：sign …」 | 签名密钥与机器人安全设置不匹配 | 飞书填「签名密钥」原文；钉钉填 SEC 开头密钥；未开启校验则留空 |
| Telegram 测试 `Unauthorized` | Bot token 错误 | 向 BotFather 重取 token；网络不可达时 baseUrl 指向镜像 |
| 通用 webhook 返回 HTTP 4xx/5xx | 接收方校验失败 | 检查自定义请求头与请求体模板；无模板时默认 JSON 结构为 `{title,content,level,source,time}` |
| 站内推送收不到 | 渠道未配置收件人（userIds 空且未开推送管理员） | 编辑渠道补收件人或开启「推送全部管理员」 |
| 重试后成功但对方收到多条 | Hangfire 至少一次语义（超时/响应解析失败会重试） | 接收方按内容幂等处理；后续可加事件 id 头 |

## 验收流程

1. `node local-dev/notification-e2e.mjs` 全绿（42/42，映射见 [tdd.md](./tdd.md)）。
2. 浏览器走查（@manual）：管理员可见侧边栏「通知中心」；渠道管理 Tab 新建/试发/编辑/删除；发送记录 Tab 过滤分页与状态/级别展示；非管理员访问 `/admin/notifications` 跳回 /apps。
