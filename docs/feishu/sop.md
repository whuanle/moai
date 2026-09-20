# feishu 飞书通知模块 操作手册（SOP）

> 关联：[SDD](./sdd.md) ｜ [BDD](./bdd.md)（验收场景编号） ｜ [TDD](./tdd.md)（验证映射与回归命令） ｜ [SOP](./sop.md)

## 1. 打通群聊/私聊回复（前置配置）

1. [飞书开放平台](https://open.feishu.cn/app) 创建企业自建应用，取 `AppID`/`AppSecret`。
2. **添加机器人能力**（应用功能 → 机器人），否则收不到消息也发不出回复。
3. 权限管理开通 `im:message`（获取与发送单聊、群聊消息）；事件与回调 → 事件订阅 → 订阅方式选「使用长连接接收事件」，添加事件「接收消息 im.message.receive_v1」。
4. 发布飞书应用新版本使能力生效。
5. 平台内由团队 Admin 打开目标应用工作台 →「外部渠道」→「接入飞书应用」，填入 AppID/AppSecret（即创建连接并自动绑定到该应用，对应 API：`POST /api/feishu_app` + `POST /api/feishu_app/{id}/bind`，`channelType=app`，`channelId=应用 id`）；连接已存在时改用「绑定已有连接」。目标应用须为**已发布且配置了对话模型的内部 Agent 应用**（未发布时渠道页会提示不处理消息）。
6. 在飞书里把机器人拉进群（或直接私聊），@机器人 或私聊发送文本消息即得到应用回复（@FS-S19）。

> 群聊默认仅 @机器人 的消息会触发事件；需要全部群消息需在飞书侧开启「接收群聊中@机器人消息」以外的权限（按飞书实际能力为准）。

## 2. 新渠道接入（扩展点，当前仅 app）

1. 业务模块 Core 引用 `MoAI.Feishu.Shared`，实现 `IFeishuEventHandler`（声明 `ChannelType`，按 `message.EventType` 过滤并自捕获异常）。
2. 在提供方模块 `ConfigureServices` 注册（参考 `AiCoreModule`：`AddSingleton<IFeishuEventHandler, AppFeishuMessageHandler>()`）。
3. `FeishuChannelType` 扩展枚举值并在 `BindFeishuAppCommandHandler.GetChannelTeamIdAsync` 补渠道校验分支。

## 3. 真实环境验收（@FS-S19~S23）

1. 绑定后列表 `isOnline=true`；飞书发送「你好」→ 数秒内收到应用回复。
2. 连续两轮提问（第二轮依赖第一轮上下文）→ 回复具备上下文连续性（@FS-S23）。
3. 发送图片 → 无回复（@FS-S20）；未配置模型的应用绑定后提问 → 收到「应用尚未配置对话模型」类文本（@FS-S22）。
4. 站内应用工作台「日志」可见该会话（外部用户、标题为首条消息摘要）。

## 4. 回归与启动观察

```bash
dotnet build src/MoAI/MoAI.csproj        # 0 error
node local-dev/feishu-e2e.mjs            # 28/28（对应 @FS-S1~S14，见 TDD）
```

启动观察（@FS-S15）：日志出现「飞书长连接初始化完成，共 N 个应用」或连接级 `WssClientError`；假凭证终态退出属预期。

## 5. 存量库升级

新库由 EnsureCreated 直接建表；存量库执行：

```bash
docker exec -i moai-postgres psql -U postgres -d moai < asserts/feishu_app.sql
```

执行后重跑 `tool/PostgresScaffold` 逆向生成，保持库与实体一致。

## 6. 排障

| 现象 | 原因 | 处理 |
|---|---|---|
| 飞书发消息无回复 | 飞书应用未添加机器人能力 / 未订阅 im.message.receive_v1 / 未开 im:message 权限；绑定目标是未发布或未配置模型的应用；连接未绑定渠道 | 按第 1 节逐项核对；列表看 `isOnline` 与绑定字段；查日志「飞书消息忽略」关键字（@FS-S21） |
| 应用渠道页「接入飞书应用」提交报 409 | 该飞书 AppID 已建过连接（同一开放平台应用全局一连，可能在其它应用页或 API 创建） | 改用「绑定已有连接」选择既有未绑定连接，或先删除原连接再接入（@FS-S25/@FS-S26） |
| 列表 `isOnline=false` 且日志 `app_id is invalid` | 凭证错误（终态） | 修正 AppSecret 重新保存（触发重连） |
| `isOnline=false` 周期性重连日志 | 网络中断/飞书侧断开 | SDK 自动重连，无需处理；持续失败查代理与域名 |
| 绑定返回 409 | 该飞书应用已绑定其它渠道（@FS-S8） | 先解绑或换一个飞书应用 |
| 建连接返回 409 | 同 AppID 已建过连接（飞书侧一应用一连） | 复用既有连接或先删除 |
| 事件收到但业务无反应 | 渠道类型无处理器注册 / 业务方未订阅该 event_type | 确认 `AddSingleton<IFeishuEventHandler,...>`；核对飞书订阅 |
| 回复成功但会话历史丢失 | Redis chat↔session 映射过期（30 天滑动） | 预期自愈行为：新会话重新开始；需要长期记忆时续期使用即自动滑动 |
| 启动报「飞书长连接初始化失败」但进程未停 | 存量库未建表等 | 执行第 5 节 DDL 后重启 |
