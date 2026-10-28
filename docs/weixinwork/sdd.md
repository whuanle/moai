# weixinwork 企业微信渠道模块 设计规格（SDD）

> 关联：[SDD](./sdd.md) ｜ [BDD](./bdd.md) ｜ [TDD](./tdd.md) ｜ [SOP](./sop.md) ｜ 上游：[../team/sdd.md](../team/sdd.md)（团队角色门禁） ｜ [../ai/sdd.md](../ai/sdd.md)（Agent 运行时） ｜ 同构模块：[../dingtalk/sdd.md](../dingtalk/sdd.md)、[../feishu/sdd.md](../feishu/sdd.md) ｜ 相关：[../oauthconnect/sdd.md](../oauthconnect/sdd.md)（企业微信扫码登录，另一领域） ｜ 证据：[local-dev/weixinwork-e2e.mjs](../../local-dev/weixinwork-e2e.mjs)、[asserts/weixinwork_app.sql](../../asserts/weixinwork_app.sql)
> 规范：[../DOC-STANDARD.md](../DOC-STANDARD.md)。行为场景见 BDD（@WX-Sxx），本文不重复。

## 目标

让企业微信成员与团队应用对话：**一个企业微信管理端自建应用（CorpId + AgentId + Secret）只建一条连接**，成员单聊应用或群里 @应用 的消息经**回调模式**（HTTP 回调 + Token 签名 + AES 加解密）统一接收，按绑定关系转发到业务模块，运行应用 Agent 后经应用消息接口把回复发回成员/群聊。整体与 [../dingtalk/sdd.md](../dingtalk/sdd.md) 钉钉模块同构（连接管理/转发/绑定/门禁/会话管线一致），差异在传输层：**企业微信自建应用无 Stream 长连接模式，回调模式需要公网可达的平台地址与 Token/EncodingAESKey 配置**。

## 组件

```
src/weixinwork/                     企业微信渠道基座（本模块）
├── MoAI.WeixinWork.Shared/       Commands/Queries + Models/WeixinWorkEventMessage + Services 契约（含 IWeixinWorkCallbackService）
├── MoAI.WeixinWork.Core/         Handlers(3) + Queries(1) + Services(加解密/回调服务/转发/去重/token/回复客户端)
└── MoAI.WeixinWork.Api/          Controllers/WeixinWorkAppController（/api/weixinwork_app）
                                   Controllers/WeixinWorkCallbackController（匿名回调端点）
src/ai/MoAI.AI.Core/              应用渠道消费者
└── Services/AppWeixinWorkMessageHandler.cs   回调消息 → Agent 会话 → 应用消息接口回复
```

基座核心服务（`MoAI.WeixinWork.Core/Services/`）：

| 服务 | 生命周期 | 职责 |
|---|---|---|
| `WeixinWorkCallbackService` | Singleton | 回调入口：加载连接 → 校验 msg_signature（sha1(sort(token,timestamp,nonce,encrypt))）→ AES 解密 → receiveid 比对 → 受理后交转发器；验证请求解密 echostr 回显明文 |
| `WeixinWorkMessageCrypt` | 静态工具 | 加解密方案实现：AES-256-CBC，密钥=EncodingAESKey 补 `=` 后 Base64 解码（32B），IV=密钥前 16B；明文=随机 16B+长度 4B 大端+报文+receiveid |
| `WeixinWorkEventForwarder` | Singleton | MsgId 去重（30 分钟内存窗口）后异步查绑定并按渠道类型投递 `IWeixinWorkEventHandler`；投递前二次校验连接未禁用/未删除 |
| `WeixinWorkEventDeduplicator` | Singleton（被转发器持有） | MsgId 内存窗口去重，吸收企业微信侧重发 |
| `WeixinWorkAccessTokenProvider` | Singleton | corp_access_token 按连接缓存（提前 5 分钟过期），凭证每次过期后从库重读；`Invalidate` 供更新与 40014/42001 失效重试 |
| `WeixinWorkApiClient` | Singleton | 回复发送：群聊（ChatId 非空）走 `appchat/send`，单聊走 `message/send`（touser+agentid）；内容按 UTF-8 字节截断 2048；实现 `IWeixinWorkApiClient` |

业务模块接入契约（`MoAI.WeixinWork.Shared/Services/`）：实现 `IWeixinWorkEventHandler`（声明 `ChannelType`，内部按 `MessageType` 过滤并自捕获异常）并在提供方模块注册（`AiCoreModule` 注册 `AddSingleton<IWeixinWorkEventHandler, AppWeixinWorkMessageHandler>()`）。事件消息为 `WeixinWorkEventMessage`（解密原文在 `Payload`，FromUserId/ChatId/Content 单列）。回调控制器经 `IWeixinWorkCallbackService`（Shared 契约）调用 Core，保持 Api→Shared 依赖单向。

### 前端入口：应用工作台「外部渠道」企业微信分区

`ui/src/pages/teams/apps/AppWeixinWorkChannelsSection.tsx`（`ui/src/api/weixinworkApp.ts` 封装 Kiota），与飞书/钉钉分区并排渲染于应用工作台「外部渠道」页，仅内部 Agent 应用的 Admin+ 可见。交互与钉钉分区一致：**只提供「接入企业微信应用」（创建连接即绑定到当前应用）、编辑、停用/启用、删除**，删除即彻底移除；表单为 名称/企业 ID/AgentId/Secret/回调 Token/EncodingAESKey（编辑时密钥留空保持不变）+ 可选 API 地址，编辑弹窗额外展示本连接的**回调地址**（`{origin}/api/weixinwork_app/{id}/callback`，一键复制填入企业微信管理端）。渠道表格只回显 `bindChannelId == appId` 的连接。

## 回复链路（AppWeixinWorkMessageHandler）

`MoAI.AI.Core` 实现（引用 WeixinWork.Shared 契约），与 AG-UI、钉钉/飞书渠道共用同一套应用 Agent 管线：

1. **过滤**：只处理 `MsgType=text`；正文去掉群聊 `@应用名 ` 前缀；`event`（进入会话等）与其它类型忽略。
2. **应用门禁**：绑定的 app 必须存在、未禁用、内部应用、Agent 类型且已发布，否则静默忽略（记日志）。
3. **会话定位**：群聊按 ChatId、单聊按成员 userid 作会话键，Redis（`weixinwork:chat:{weixinWorkAppId}:{会话键}`，30 天滑动）映射到 `app_agent_session`；未命中则新建会话（`UserType=External`）。
4. **运行**：`AppAgentFactory.CreateAsync(appId, teamId, userId:0, sessionId, isDebug:false)` 装配 Agent，加载热态快照 → `RunAsync` → 提取最后一条 Assistant 文本。
5. **回复与落库**：回复经 `IWeixinWorkApiClient.SendTextMessageAsync` 发回（群聊 appchat/send、单聊 message/send，超长按 UTF-8 字节截断 2048）；会话序列化回热态 + `AppChatFlushService.FlushAsync` 落库。业务异常（如未配置模型）把异常消息作为回复发给用户。
6. **串行化**：同一会话键用进程内信号量串行；企业微信成员不映射内部用户（`userId=0`），用量计入团队/应用维度。

## 数据

- `weixinwork_app`（[asserts/weixinwork_app.sql](../../asserts/weixinwork_app.sql)）：企业微信应用连接。`(corp_id, agent_id)` 联合唯一（未删除行）——同一企业的同一自建应用全局只能建一条连接（避免同一应用多处接入互抢回调）。`token`/`encoding_aes_key` 存回调配置（签名校验与加解密），`api_endpoint` 存可选 API 地址覆盖（空=官方 `https://qyapi.weixin.qq.com`，供桩联调/代理）。
- `weixinwork_app_binding`：渠道绑定。**应用渠道独占**：`(weixinwork_app_id) WHERE is_deleted=0 AND channel_type=0` 唯一；另建 `(weixinwork_app_id, channel_type, channel_id)` 唯一防重复绑定。渠道用 `(WeixinWorkChannelType, string ChannelId)` 弱关联（`app` → app.id）。
- 权限模型沿用团队资源：写操作 Admin+，读列表 Member+。接入 = `POST /api/weixinwork_app` 携带 `channelType=app` + `channelId` 创建即绑定（无独立绑定端点），删除即级联解绑。
- 回调端点 `[AllowAnonymous]`（`/api/weixinwork_app/callback/{id}`），由企业微信服务器调用；安全性依赖签名校验 + AES 解密 + receiveid 比对三重防护，连接不存在返回 404。

## 关键决策

1. **回调模式而非长连接**：企业微信自建应用官方仅提供 HTTP 回调接收（`智能机器人` 的 WebSocket 通道是另一对象，不适用自建应用消息）；与钉钉/飞书的差异集中于传输层，连接管理/绑定/门禁/会话管线照旧同构，不抽象公共基座。
2. **受理即应答、业务异步**：回调 5 秒应答窗口内只做验签+解密+去重后立即回 `success`（不被动回复），Agent 运行后走应用消息接口主动回复——与钉钉「ack 后转线程池」同构。
3. **回复走应用消息接口而非被动回复**：被动回复需在应答窗口内给出加密报文且无法承载长耗时 Agent 输出；`message/send`（单聊）/`appchat/send`（群聊）免推送体依赖，token 缓存 + 40014/42001 失效重试一次。
4. **receiveid 比对**：解密明文尾部的 receiveid（企业 ID）与连接配置比对，防止回调 URL 被探测/串扰。
5. **可选 API 地址（`api_endpoint`）**：连接级覆盖 API 域名（默认官方），E2E 桩与代理场景零配置注入——沿用飞书 `domain` 字段模式。
6. **在线语义**：回调模式下连接启用即可接收（回调端点随宿主常驻），`IsOnline = !IsDisable`；无长连接生命周期可展示。
7. **企业微信成员不映射内部用户**：`userId=0`，会话 `UserType=External`；与钉钉/飞书渠道一致。

## 已知问题

- 仅支持文本消息；非文本（图片/文件/语音等）与 `event` 推送受理后忽略。Agent 回复为 Markdown 原文按 text 发送。
- 平台必须公网可达才能配置回调（内网部署需反代或内网穿透）；服务器 IP 需按企业微信提示配置「企业可信 IP」。
- 群聊回复依赖 `appchat/send`（应用需在群里；企业微信平台限制下部分存量群可能拒绝），失败记日志本轮不回复。
- 去重窗口为单实例内存（30 分钟/4096 条），多实例各实例独立去重（回调为单投递，重试跨实例场景罕见）；进程内串行锁不跨实例。
- 回调模式无连接对象，「状态」列的在线仅表示连接启用；Token/EncodingAESKey 配置错误只能在推送失败日志中暴露。
- 应用列表卡片不展示企业微信入口图标：企业微信无拉起应用会话的公开深链（飞书 applink/钉钉 jumprobot 的对应物不存在）。
