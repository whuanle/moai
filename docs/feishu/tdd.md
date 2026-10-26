# feishu 飞书通知模块 验证映射（TDD）

> 关联：[SDD](./sdd.md) ｜ [BDD](./bdd.md) ｜ [TDD](./tdd.md) ｜ [SOP](./sop.md)
> 回归命令：后端运行中执行 `node local-dev/feishu-e2e.mjs`（存量库需先执行 [asserts/feishu_app.sql](../../asserts/feishu_app.sql)）。

| 场景 | 验证物 | 结果（日期） |
|---|---|---|
| @FS-S1~S5 | local-dev/feishu-e2e.mjs（FS-01~05） | PASS 32/32（2026-10-08） |
| @FS-S6~S11 | local-dev/feishu-e2e.mjs（FS-06a~f、07~11；创建即绑定校验、端点下线 405） | PASS 32/32（2026-10-08） |
| @FS-S12 | local-dev/feishu-e2e.mjs（FS-12a~c；FS-12d~g 编辑换 AppID 见 2026-10-26 证据摘要） | PASS 42/42（2026-10-26） |
| @FS-S13~S14 | local-dev/feishu-e2e.mjs（FS-13~14；删除即彻底移除、渠道可重建接入） | PASS 32/32（2026-10-08） |
| @FS-S29~S30 | local-dev/feishu-e2e.mjs（FS-15a~b、16a~d；应用列表回显外部渠道与 applink 地址、禁用隐藏/启用恢复） | PASS 38/38（2026-10-26） |
| @FS-S15 | 代码走查 + 启动日志（假凭证连接终态退出不阻断宿主，见 sop.md 第 4 节） | PASS（2026-09-14） |
| @FS-S16~S18 | @manual（需真实飞书应用，走查 FeishuEventForwarder） | PASS 走查（2026-09-14） |
| @FS-S19~S23 | @manual（需真实飞书应用 + 已配置模型的应用，验收步骤见 sop.md 第 3 节） | 待真实环境验收 |
| @FS-S24~S25、S27~S28 | ui/src/pages/teams/apps/__tests__/AppChannelsSection.test.tsx、__tests__/AppWorkspace.test.tsx（`cd ui && npm run test`） | PASS 498/498（2026-10-08；@FS-S26 已随绑定/解绑下线退役） |

## 证据摘要（2026-09-14）

- 构建：`dotnet build src/MoAI/MoAI.csproj` → 0 error（含 AI.Core 的 AppFeishuMessageHandler）。
- 存量库：`asserts/feishu_app.sql` 已应用于开发库（feishu_app / feishu_app_binding 两表 + 5 索引建齐）。
- E2E：`node local-dev/feishu-e2e.mjs` → 28 pass / 0 fail（鉴权、校验、CRUD、绑定互斥、非法渠道类型 400、级联解绑）。
- OpenAPI：`/openapi/v1.json` 出现全部 5 个 feishu_app 路由（供 syncapi）。
- 长联通路：假凭证连接真实请求飞书 endpoint 收到 `app_id is invalid`，SDK 终态退出、仅记录日志，无未处理异常。
- 回复链路走查：AppFeishuMessageHandler 复用 AppAgentFactory/热态快照/AppChatFlushService（与 AG-UI 同管线），chat↔session 映射 `feishu:chat:*` 30 天滑动；发送走 FeishuApiClient（tenant_access_token 缓存）。

## 证据摘要（2026-09-19，外部渠道页）

- 前端：`npm run typecheck` / `npm run lint` 0 error（8 个 warning 均为存量文件）；`npm run test` → 361/361 PASS（含 AppChannelsSection 9 用例与 AppWorkspace 菜单可见性断言）。
- 后端：`dotnet build src/MoAI/MoAI.csproj` → 0 error 0 warning（本轮后端零改动，Kiota 客户端存量已含 feishu_app 全部路由，无需重新 syncapi）。

## 证据摘要（2026-10-08，外部渠道简化：创建即绑定，下线绑定/解绑）

- 后端：`CreateFeishuAppCommand` 增加可选 `channelType/channelId`（创建即绑定、同一事务落库）；`FeishuAppController` 下线 `POST /{id}/bind`、`POST /{id}/unbind` HTTP 端点（命令保留给知识库外部源内部 MediatR 复用）。`dotnet build src/MoAI/MoAI.csproj` → 0 error。
- 前端：`AppChannelsSection` 仅保留接入（单次创建调用）、停用/启用、删除；删除「绑定已有连接」弹窗与「解绑」操作；i18n 同步清理 zh-CN/en-US。`npm run typecheck`/`npm run lint` 0 error（9 个 warning 均为存量文件）；`npm run test` → 498/498 PASS（AppChannelsSection 7 用例）。
- syncapi：独立构建后端（5002 端口）重新生成 Kiota 客户端，`feishu_app` 的 bind/unbind 路径移除、create 请求体新增 `channelType/channelId`。
- E2E：`node local-dev/feishu-e2e.mjs`（对 5002 新构建）→ 32 pass / 0 fail（创建即绑定校验 404/403/400、同 AppID 互斥 409、bind/unbind 端点 405、删除即彻底移除后同 AppID 重建接入成功）。

## 证据摘要（2026-10-26，应用列表外部渠道入口）

- 后端：`MoAI.App.Core` 新增 `AppExternalChannelHelper`——从 `feishu_app_binding` 应用渠道反查启用的飞书连接（`ChannelId` 落库为原始字符串，内存解析 Guid 比对避免大小写漏配；禁用连接不回显），applink 域名由连接域名推导（`://open.` → `://applink.`，未知域名回退 `applink.feishu.cn`）；`AppItem` 新增 `ExternalChannels`（channelType 枚举 + chatUrl），团队列表 `GET /app/list` 与公开列表 `GET /app/public/list` 映射带出。`dotnet build src/MoAI/MoAI.csproj` → 0 error。
- E2E：`node local-dev/feishu-e2e.mjs` → 38 pass / 0 fail（新增 FS-15a~b 回显 applink 地址、FS-16a~d 禁用隐藏/启用恢复）。
- 前端：`AppPlaza` 与 `TeamApps` 卡片右上角按 `externalChannels` 渲染 `ExternalChannelEntry`（内联官方 logo 的 `FeishuIcon`，Tooltip + 新窗口打开 chatUrl）；i18n `appManage.openInFeishu`（zh/en）。typecheck/lint 0 error（既有 warning 与本次无关），TeamApps/AppPlaza/AppConfigSection 相关测试全绿；浏览器走查两类卡片图标与点击地址正确（applink 弹窗被内嵌测试浏览器拦截，真实浏览器唤起飞书）。

## 证据摘要（2026-10-26，编辑连接：可修改飞书 AppID）

- 后端：`UpdateFeishuAppCommand` 增加可选 `appId`（留空保持不变），Handler 全局唯一校验（排除自身）后赋值并触发重连；`dotnet build src/MoAI/MoAI.csproj -o .builds/dingtalk` → 0 error。
- 前端：`AppChannelsSection` 操作列新增「编辑」（编辑弹窗复用创建表单，AppSecret 留空=保持不变、必填规则仅创建态生效），启用/停用不带 AppID 避免并发覆盖；i18n zh/en +4 键；`AppChannelsSection.test.tsx` 增编辑 2 用例。
- E2E：`node local-dev/feishu-e2e.mjs`（对 5002 新构建）→ 42 pass / 0 fail（FS-12d 换 AppID、12e 回显、12f 占用 409、12g 留空保持）。

| @FS-S31 | 双实例实测（与钉钉共用 RedisAppLease 同轮验证）：飞书应用分散到两实例（公平份额释放 + 争抢）、杀一实例后另一实例约 40s 接管其应用 | PASS（2026-10-26） |
