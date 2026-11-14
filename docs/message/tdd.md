# 站内信与事件推送（Message，MSG）验证映射（TDD）

> 关联：[SDD](./sdd.md) ｜ [BDD](./bdd.md)（场景定义） ｜ [TDD](./tdd.md) ｜ [SOP](./sop.md)
> 本文只做「场景 → 验证物 → 结果」映射；场景语义见 BDD 编号，设计依据见 SDD。

## 场景映射表

| 场景 | 验证物 | 结果（日期） |
|---|---|---|
| @MSG-S1、@MSG-S2 | [local-dev/message-e2e.mjs](../../local-dev/message-e2e.mjs)（MSG-01/02，长轮询裸客户端） | PASS（2026-11-01） |
| @MSG-S3、@MSG-S4 | 同上（MSG-03/04） | PASS（2026-11-01） |
| @MSG-S5、@MSG-S9 | 同上（MSG-05/09） | PASS（2026-11-01） |
| @MSG-S6 | 同上（MSG-06，RabbitMQ 管理口发布 message.push → 消费者 → onEvent） | PASS（2026-11-01） |
| @MSG-S7 | 同上（MSG-07，双用户隔离） | PASS（2026-11-01） |
| @MSG-S8 | 同上（MSG-08，同用户双连接） | PASS（2026-11-01） |
| @MSG-S10 | [tests/MoAI.Message.Tests](../../tests/MoAI.Message.Tests)（MessageServiceTests.SendToUsers_*） | PASS 13/13（2026-11-01） |
| @MSG-S11 | 同上（MessageServiceTests.SendToTeam_*） | PASS（2026-11-01） |
| @MSG-S12 | 同上（MessageServiceTests.SendToAllUsers_SkipDisabledUsers） | PASS（2026-11-01） |
| @MSG-S13 | 同上（MessageHandlerTests.Read*/Delete*） | PASS（2026-11-01） |
| @MSG-S14 | [ui/src/pages/account/__tests__/Messages.test.tsx](../../ui/src/pages/account/__tests__/Messages.test.tsx) + 浏览器走查（角标 2→1→0 实时变化） | PASS 6/6（2026-11-01） |
| @MSG-S15 | 浏览器走查（MQ 管理口发事件 → 右上角 Notification 带标题悬浮，刷新后消失） | PASS（2026-11-01） |
| @MSG-S16 | 浏览器走查（/account/messages 二级菜单高亮，/account 重定向） | PASS（2026-11-01） |
| @MSG-S17 | 同上 vitest（点击已读/删除气泡确认；「全部已读」按钮已移除）+ 浏览器走查 | PASS（2026-11-02 复验） |
| @MSG-S18 | 浏览器直查 + E2E 回归：unread-count 按登录态统计（修复 Controller 内自建命令丢失 ContextUserId） | PASS（2026-11-01） |
| @MSG-S19 | [ui/src/pages/account/__tests__/Messages.test.tsx](../../ui/src/pages/account/__tests__/Messages.test.tsx)（页头批量已读/全选批量删除只删已读·未读忽略/仅勾未读删除禁用/选择清空/默认 20 条/无全部已读按钮）+ 浏览器走查（勾选 2 条批量删除 44→42、全选气泡文案、页面级无滚动） | PASS 11/11（2026-11-02；ui typecheck 0、lint 0、vitest 全量 711/711） |

说明：

- 前端实时链路联调中排障落定：`@microsoft/signalr` 默认 `withCredentials: true` 与后端 `AllowAnyOrigin` CORS 冲突（negotiate 瞬间 Failed to fetch），已按官方建议关闭（平台为 token 鉴权不依赖 cookie）；见 [SOP](./sop.md) 排障表。
- MQ 场景（@MSG-S6~S8）经 RabbitMQ 管理 HTTP API 投递测试事件，管理口不可达时自动 SKIP（环境门控），其余用例不受影响。
- 「落库→已读流转」的完整业务闭环需首个发送方模块接入后补 E2E 场景；当前发送链路由单测覆盖（无发送 API 可触发真实落库）。
- 多实例跨节点推送（backplane）为部署级行为，验证步骤见 [SOP](./sop.md) 第 4 节。

## 回归入口

```bash
dotnet build src/MoAI/MoAI.csproj        # 0 error
dotnet test tests/MoAI.Message.Tests     # 单测 13/13
# 后端运行中（Postgres/Redis/RabbitMQ 可达）：
node local-dev/message-e2e.mjs
```
