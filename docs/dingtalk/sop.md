# dingtalk 钉钉渠道模块 操作手册（SOP）

> 关联：[SDD](./sdd.md) ｜ [BDD](./bdd.md)（验收场景编号） ｜ [TDD](./tdd.md)（验证映射与回归命令） ｜ [SOP](./sop.md)

## 1. 打通单聊/群聊回复（前置配置）

1. [钉钉开放平台](https://open.dingtalk.com/) 创建**企业内部应用**，取凭证与基础信息里的 `AppKey`（新版后台为 Client ID）与 `AppSecret`（Client Secret）。
2. **添加应用能力 → 机器人**，否则收不到消息也发不出回复；机器人消息接收模式选择「**Stream 模式**」（无需公网地址、无需加解密配置）。
3. **版本管理与发布**：发布应用使机器人能力生效。
4. 平台内由团队 Admin 打开目标应用工作台 →「外部渠道」→「钉钉渠道」→「接入钉钉应用」，填入 AppKey/AppSecret 即创建连接并绑定到该应用（对应 API：`POST /api/dingtalk_app` 携带 `channelType=app`、`channelId=应用 id`，创建即绑定、无独立绑定端点）；该钉钉 AppKey 已建过连接时须先删除原连接再接入。目标应用须为**已发布且配置了对话模型的内部 Agent 应用**（未发布时渠道页会提示不处理消息）。
5. 在钉钉里单聊机器人，或在机器人所在群里 @机器人 发送文本消息即得到应用回复（@DT-S19）。群聊中钉钉仅把 @机器人 的消息推给应用（平台行为）。

## 2. 新渠道接入（扩展点，当前仅 app）

1. 业务模块 Core 引用 `MoAI.DingTalk.Shared`，实现 `IDingTalkEventHandler`（声明 `ChannelType`，按 `message.Topic` 过滤并自捕获异常）。
2. 在提供方模块 `ConfigureServices` 注册（参考 `AiCoreModule`：`AddSingleton<IDingTalkEventHandler, AppDingTalkMessageHandler>()`）。
3. `DingTalkChannelType` 扩展枚举值（订阅型渠道一对多时参考飞书 `feishu_app_wiki_channel.sql` 的分层互斥索引改造）。

## 3. 真实环境验收（@DT-S19~S23）

1. 绑定后列表 `isOnline=true`；钉钉单聊发送「你好」→ 数秒内收到应用回复。
2. 连续两轮提问（第二轮依赖第一轮上下文）→ 回复具备上下文连续性（@DT-S23）。
3. 发送图片 → 无回复（@DT-S20）；未配置模型的应用绑定后提问 → 收到「应用尚未配置对话模型」类文本（@DT-S22）。
4. 站内应用工作台「日志」可见该会话（外部用户、标题为首条消息摘要）。

## 4. 回归与启动观察

```bash
dotnet build src/MoAI/MoAI.csproj        # 0 error
node local-dev/dingtalk-e2e.mjs          # 33/33（对应 @DT-S1~S14，见 TDD）
```

启动观察（@DT-S15）：日志出现「钉钉长连接初始化完成，共 N 个应用」与「开始启动新的【钉钉Stream客户端】」；假凭证网关返回 `authFailed` 属预期（仅记错误日志，SDK 内部重试）。

## 5. 存量库升级

新库由 EnsureCreated 直接建表；存量库执行：

```bash
docker exec -i moai-postgres psql -U postgres -d moai < asserts/dingtalk_app.sql
```

## 6. 排障

| 现象 | 原因 | 处理 |
|---|---|---|
| 钉钉发消息无回复 | 应用未添加机器人能力 / 接收模式不是 Stream 模式 / 未发布；接入目标是未发布或未配置模型的应用 | 按第 1 节逐项核对；列表看 `isOnline`；查日志「钉钉消息忽略」关键字（@DT-S21） |
| 应用卡片没有钉钉入口图标，或点击只打开钉钉、不打开机器人 | 图标链接需要机器人的加密 dingtalk id，钉钉仅在机器人消息回调中下发；未回填前不露出（历史版本的 AppKey 兜底链接只能打开钉钉，已移除） | 确认连接 `isOnline=true` 后，在钉钉内搜索机器人名称并发送任意消息 → 转发器回填 `chatbot_user_id` → 图标自动出现（@DT-S27） |
| 「接入钉钉应用」提交报 409 | 该钉钉 AppKey 已建过连接（同一开放平台应用全局一连，可能在其它应用页创建） | 先删除原连接（删除即彻底移除）再接入，或换一个钉钉应用（@DT-S9） |
| 列表 `isOnline=false` 且日志 `authFailed` | 凭证错误 | 「编辑」连接修正 AppKey/AppSecret 后保存（触发重连），无需删除重建（@DT-S12） |
| `isOnline=false` 周期性重连日志 | 网络中断/钉钉网关侧断开 | SDK 自动重连，无需处理；持续失败查网络与代理 |
| 消息已读但不回复，日志见「钉钉消息发送失败：msgtype is null」 | webhook 回复体缺 msgtype 字段（历史版本缺陷，已修复） | 更新到含修复的构建即可 |
| 有回复但同一消息被处理两次 | ack 超时（处理超 5 秒）触发钉钉重发 | message_id 去重器应吸收；若重启窗口内重发，属预期幂等边界 |
| 回复成功但会话历史丢失 | Redis conversationId↔session 映射过期（30 天滑动） | 预期自愈行为：新会话重新开始 |
| 启动报「钉钉长连接初始化失败」但进程未停 | 存量库未建表等 | 执行第 5 节 DDL 后重启 |
