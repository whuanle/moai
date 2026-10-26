# 动态插件（DynamicPlugin）设计规格（SDD）

> 关联：[SDD](./sdd.md) ｜ [BDD](./bdd.md) ｜ [TDD](./tdd.md) ｜ [SOP](./sop.md) ｜ 上游：[../aiplugin-static/sdd.md](../aiplugin-static/sdd.md) ｜ 规范：[../cqrs-conventions.md](../cqrs-conventions.md) ｜ 证据：[local-dev/dynamic-plugin-e2e.mjs](../../local-dev/dynamic-plugin-e2e.mjs) ｜ [local-dev/bocha-search-e2e.mjs](../../local-dev/bocha-search-e2e.mjs) ｜ [local-dev/moji-weather-e2e.mjs](../../local-dev/moji-weather-e2e.mjs)
> 规范：[../DOC-STANDARD.md](../DOC-STANDARD.md)。行为场景见 BDD（@DYN-Sxx），本文不重复。

## 目标

在已有 `aiplugin` 插件引擎（`PluginRegistry` + `PluginExecutor`）与静态插件能力之上，落地**动态插件**实例化管理。动态插件模板（如 `dynamic_greet`）存在于内存注册表，无实例时不可直接运行；需**先创建实例并填入配置**，一个模板可创建多个实例，实例 key 全局唯一。

需要实现：
1. **动态插件实例列表**：查询已创建的实例（DB `plugin_dynamic ⋈ plugin`），展示实例 key、模板 key、标题、分类、配置。
2. **创建/编辑实例**：填实例 key（小写+下划线、≤30、全局唯一）、模板 key、标题、描述、分类、配置（Monaco 编辑器）。创建时可校验配置；运行实例取存储的配置。
3. **运行实例**：`key=实例 key`，后端按实例 key 定位模板与配置并执行。
4. **删除实例**：软删除实例及关联 plugin 行。

## 组件

```
src/aiplugin/
├── MoAI.AIPlugin.Shared/
│   ├── Commands/SaveDynamicPluginCommand.cs        {pluginKey,templeteKey,title,description,classifyId,config,avatarPath?} → EmptyCommandResponse
│   ├── Commands/DeleteDynamicPluginCommand.cs       {pluginKey} → EmptyCommandResponse
│   ├── Commands/UpdatePluginAvatarCommand.cs        {pluginId(路由回填),objectKey} → EmptyCommandResponse
│   └── Queries/Responses/QueryPluginManageListCommandResponseItem.cs  （增强：+TempleteKey,+Config,+ConfigExample,+AvatarPath）
├── MoAI.AIPlugin.Dynamic/                            内置模板宿主（Models/ 各模板模型 + Plugins/ 模板实现）
├── MoAI.AIPlugin.Core/
│   ├── Commands/RunPluginCommandHandler.cs          （增强：key 未命中注册表时走实例解析器）
│   └── Services/IDynamicInstanceResolver.cs         {Resolve(instanceKey) → (Template,ConfigJson)}
├── MoAI.AIPlugin.Custom/
│   ├── Commands/SaveDynamicPluginCommandHandler.cs  校验 + 创建/更新实例
│   ├── Commands/DeleteDynamicPluginCommandHandler.cs 软删除
│   ├── Commands/UpdatePluginAvatarCommandHandler.cs 校验文件已登记 → plugin.avatar_path = objectKey
│   ├── Services/DynamicInstanceResolver.cs          实例 key → 模板 key + 配置
│   ├── Queries/QueryPluginManageListCommandHandler.cs （增强：动态实例合并 + 模板字段填充）
│   └── CustomPluginModule.cs                        注册 IDynamicInstanceResolver
└── MoAI.AIPlugin.Api/
    ├── Controllers/DynamicPluginController.cs       [Route("/ai/plugin/dynamic")]，门禁在 Controller
    └── Controllers/PluginManageController.cs        +POST {id}/avatar（管理员，跨 custom/dynamic/static 共用）

ui/src/
├── api/plugin.ts                                    +getDynamicTemplates,+saveDynamicPlugin,+deleteDynamicPlugin,+updatePluginAvatar
└── pages/plugins/
    ├── DynamicPluginPanel.tsx                        实例列表 + 运行 + 授权 + 新建/编辑（共享模态）+ 列表头像展示
    ├── PluginTemplates.tsx                           模板列表页（系统/团队双页面：/plugin/templates 与 /team/{teamId}/plugin/templates；卡片实例数 + 卡片「新建」+ 面包屑返回）
    └── components/DynamicPluginInstanceModal.tsx     系统/团队共用创建/编辑模态（kg 绑定预填；头像仅系统侧：新建态暂存随保存提交、编辑态即登记；团队新建实例 key 自动填「模板key_团队id」）
```

三层依赖：`Api → Core → Shared`；Core 增 `IDynamicInstanceResolver`（供 Run 解析）。Api 引用 `MoAI.Account.Shared`、`MoAI.AIPlugin.Shared`。

## API 契约

路由前缀 `/ai/plugin/dynamic`，认证自动追加 `[Authorize]`；管理员门禁在 Controller 层（`GetUserStateAsync().IsAdmin`，否则 403「只有管理员可以管理插件」）。

| 方法 | 路由 | 门禁 | 说明 |
|---|---|---|---|
| POST | `/api/ai/plugin/dynamic/save` | admin | `{pluginKey,templeteKey,title,description,classifyId,config}` → `EmptyCommandResponse`，创建/更新实例 |
| DELETE | `/api/ai/plugin/dynamic` | admin | `{pluginKey}` → `EmptyCommandResponse`，软删除实例 |
| POST | `/api/ai/plugin/run`（沿用 `PluginController`） | admin | `{key=实例key,requestJson}` → `PluginRunResult`，运行实例 |
| GET | `/api/ai/plugin`（`QueryAll`） | admin | 返回注册表模板列表，含 `configExample/paramsExample/isDynamic`（前端模板下拉） |
| GET | `/api/ai/plugin/manage/list` | admin | 动态实例列表（`kind=dynamic`），带 `templeteKey/config/configExample`；**仅系统侧记录**（`team_id=0`），团队自有实例不进此列表（@DYN-S70） |
| POST | `/api/ai/plugin/manage/{id}/avatar` | admin | `{objectKey}` → `EmptyCommandResponse`，设置插件头像（`plugin.avatar_path`，custom/dynamic/static 共用） |

> 控制器 `[Route("/ai/plugin/...")]` 之上还有全局 `/api` 路由前缀，实际路径以 `/api/ai/plugin/...` 为准（早期文档漏写 `/api`，2026-09-11 依 OpenAPI 实测修正）。

`QueryPluginManageListCommandResponseItem` 新增字段：
- `templeteKey`（string?）：动态实例的模板 key。
- `config`（string?）：动态实例存储的配置 JSON。
- `configExample`（string?）：动态模板配置示例（创建时 Monaco 初始值）。
- `avatarPath`（string）：头像存储 ObjectKey（前端拼 `{server}/static/{objectKey}` 展示；内存发现的静态插件为空串）。

## 关键决策

1. **实例 key 存 `plugin_dynamic.plugin_key`**；**模板 key 存 `plugin_dynamic.templete_key`**（`.NET` 字段 `TempleteKey`；用户新增该列）。`plugin` 行 `PluginName`=实例 key、`PluginId`→`plugin_dynamic.Id`、`Type=NativePlugin`、`IsSystem=true`。
2. **key 唯一性**：实例 key 全小写+下划线（`^[a-z_][a-z0-9_]*$`），≤30。`save` 是 **upsert**：同实例 key 再次提交按更新处理（不新建、不报冲突，见 @DYN-S5）；仅当实例 key 与**注册表 key**（静态/动态模板 key）重名时 409「实例 Key 已被使用」。
3. **模板校验**：`templeteKey` 必须在注册表且 `IsDynamic`，否则 404「动态插件模板不存在」。
4. **运行解析**：`RunPluginCommand` 的 `key` 先查注册表；未命中则用 `IDynamicInstanceResolver.Resolve(key)` 由 `plugin_dynamic.plugin_key`→`templete_key`→`registry.Get(templete_key)`，取该实例 `config` 初始化；`configJson` 无需前端传。
5. **编辑不可改实例 key**：更新只改 `templeteKey/config/title/description/classifyId`；实例 key 作为主键定位。系统侧 save/delete 只允许操作 `team_id=0` 的系统实例，命中团队自有实例（`team_id≠0`）时 403（@DYN-S70，团队实例走 teamplugin 链路）。
6. **分类校验**：`classifyId` 非 0 需在 `classify` 表存在且 `Type=plugin`，否则 400。
7. **删除**：软删除 `plugin_dynamic` 与该实例关联的 `plugin` 行（`IsDeleted=1`）。
8. **前端**：动态 Tab 用 `DynamicPluginPanel`；运行复用 `PluginRunDrawer`（`paramsExample` 来自模板）。模板列表页 `PluginTemplates` 双模式（无 `teamId`=系统/管理员，带 `?teamId=`=团队/可管理成员，非可管理成员重定向回团队插件）；**实例数前端聚合**（系统侧 manage/list 系统侧实例、团队侧 team list 过滤 `isTeamOwned`），不设后端聚合端点；创建/编辑表单收敛为共享 `DynamicPluginInstanceModal`（scope 决定保存端点、kg 绑定与头像能力）。i18n zh/en 同步。
9. **头像走全站统一 objectKey 管线**（与用户/应用/团队/wiki/知识图谱/提示词头像同模式）：前端 `uploadImageWithKey` 直传公开图片（`public/images/{sha256}.{ext}`）→ `POST /ai/plugin/manage/{id}/avatar` 只登记 `objectKey`；Handler 校验 `Files` 表 `ObjectKey+IsUploaded`（404 防伪造），**不删除旧头像文件**（与既有头像端点一致）。端点挂在 `PluginManageController`（跨 custom/dynamic/static 共用一个端点，`plugin` 行主键 `Id` 定位）；静态侧说明见 [../aiplugin-static/sdd.md](../aiplugin-static/sdd.md)。
10. **创建态头像**：插件记录保存前无 id 可登记，保存命令带可选 `avatarPath`（创建直接落库；更新仅传了才覆盖，不传不改动），Handler 同样校验 `Files` 表已登记（404 防伪造，校验先于任何写入，失败不建实例）。前端共享模态在系统侧新建态渲染 `PluginAvatarPendingUpload`（仅直传暂存 objectKey，随保存提交），编辑态保留 `PluginAvatarUpload`（选图即登记）；团队侧保存命令无此字段、暂不开放（团队列表亦未展示头像）。

## 内置动态模板

模板 key 全局唯一、发布后不可变；「模板」只描述代码能力，「实例」才承载 API Key 等敏感配置（存 `plugin_dynamic.config`）。

| 模板 key | 实现 | 配置（TConfig） | 请求（TRequest） | 响应（TResponse） |
|---|---|---|---|---|
| `dynamic_greet` | `DynamicGreetPlugin` | `Prefix` | `Name` | `Message` |
| `bocha_web_search` | `BoChaWebSearchPlugin` | `ApiKey`（必填，`InitAsync` 校验） | `Query`(必填)/`Freshness`/`Summary`/`Count`/`Include`/`Exclude` | `Query`/`TotalEstimatedMatches`/`SomeResultsRemoved`/`WebPages[]`/`Images[]` |
| `bocha_ai_search` | `BoChaAiSearchPlugin` | `ApiKey`（必填，`InitAsync` 校验） | `Query`(必填)/`Freshness`/`Include`/`Count`/`Answer` | `Query`/`ConversationId`/`Answer`/`FollowUps[]`/`WebPages[]`/`Images[]`/`ModelCards[]`/`SomeResultsRemoved` |
| `feishu_webhook_text` | `FeishuWebhookTextPlugin` | `WebhookKey`（必填，`InitAsync` 校验）/`SignKey` | `Text`（必填，`RunAsync` 校验） | `Code`/`Msg`/`Text` |
| `javascript_executor` | `JavaScriptExecutorPlugin` | `JavaScriptCode`（必填，`InitAsync` 校验；必须导出 `run(parameter)`） | `Parameters`（必填字符串，`RunAsync` 透传给 `run`） | `Parameters`/`ResultJson`/`ResultKind`（值类型见 `JsResultKind`） |
| `postgres_query` | `PostgresQueryPlugin` | `Host`（必填，`InitAsync` 校验）/`Port`(1-65535，默认 5432)/`Database`（可选）/`Username`（必填，`InitAsync` 校验）/`Password`（可选）/`MaxRows`(1-1000，默认 100)/`CommandTimeoutSeconds`(1-300，默认 30) | `Sql`（必填，`RunAsync` 校验为单条只读语句） | `Columns[]`/`Rows[]`/`RowCount`/`Truncated` |
| `mysql_query` | `MysqlQueryPlugin` | 同 `postgres_query`（`Port` 默认 3306） | 同上 | 同上 |
| `paddleocr_ocr` | `PaddleOcrPlugin` | `ApiUrl`（必填，`InitAsync` 校验；用户自有 PaddleOCR 服务地址）/`Token`（留空表示部署未开启鉴权） | `File`(必填，URL 或 Base64)/`FileType`/`UseDocOrientationClassify`/`UseDocUnwarping`/`UseTextlineOrientation` | `Pages[]`：每页含 `Text`（`rec_texts` 按行拼接）、`OcrImage`（Base64）、`InputImage`（Base64） |
| `paddleocr_structure_v3` | `PaddleStructureV3Plugin` | 同 `paddleocr_ocr` | `File`(必填)/`FileType`/`UseDocOrientationClassify`/`UseDocUnwarping`/`UseTableRecognition`/`UseFormulaRecognition`/`UseSealRecognition`/`UseChartRecognition`/`UseRegionDetection` | `Pages[]`：每页含 `PrunedResultJson`（原文 JSON）、`OutputImages`（按名索引的 Base64 字典）、`InputImage`、`SealTexts[]`（从 `seal_res_list.rec_texts` 逐条抽取） |
| `paddleocr_vl` | `PaddleVlPlugin` | 同 `paddleocr_ocr` | `File`(必填)/`FileType`/`UseDocOrientationClassify`/`UseDocUnwarping`/`UseLayoutDetection`/`UseChartRecognition`/`PrettifyMarkdown`/`ShowFormulaNumber` | `Pages[]`：每页含 `MarkdownText`、`MarkdownImages`（相对路径 → Base64）、`InputImage`、`PrunedResultJson` |
| `moji_weather` | `MojiWeatherPlugin` | `AppCode`（必填，`InitAsync` 校验）/`Token`（部分服务规格要求） | `CityId` 或 `Lat`+`Lon`（二选一，`RunAsync` 校验） | `City`/`Condition`/`Forecast[]` |
| `http_probe` | `HttpProbePlugin` | `TimeoutSeconds`(1-120)/`BodyPreviewChars`(0-4096)/`AllowPrivateNetwork`（默认 false） | `Mode`(http/tcp/dns)/`Url`/`Method`(GET/HEAD)/`Host`/`Port` | `Mode`/`Target`/`Ok`/`Error`/`ElapsedMs`/`StatusCode`/`BodyPreview`/`RemoteAddress`/`Addresses[]` |
| `zabbix_query` | `ZabbixQueryPlugin` | `BaseUrl`(必填，支持子路径)/`Token` 或 `Username`+`Password`/`UseHeaderAuth`/`TimeoutSeconds`/`MaxListItems` | `Mode`(version/problems/hosts/triggers)/`SeverityMin`(0-5)/`SearchHost`/`HostGroupIds` | `Version`/`Problems[]`（富化主机名）/`Hosts[]`/`Triggers[]`/`Truncated` |
| `redis_query` | `RedisQueryPlugin` | `Host`(必填)/`Port`(1-65535)/`Username`/`Password`/`Ssl`/`Database`(0-15)/`TimeoutSeconds`/`MaxListItems` | `Mode`(info/dbsize/slowlog/client_list/config_get/key_info)/`Section`/`Count`/`Pattern`/`Key` | `Sections`/`DbSize`/`SlowLog[]`/`Clients[]`/`Config`/`KeyInfo`/`Truncated` |
| `ssh_executor` | `SshExecutorPlugin` | `Host`(必填)/`Port`(默认 22)/`Username`(必填)/`Password` 或 `PrivateKey`（PEM/OpenSSH，私钥优先）/`CommandWhitelist`(必填前缀列表)/`CommandTimeoutSeconds`/`MaxOutputChars` | `Command`(必填，单条) | `Command`/`ExitStatus`/`Ok`/`Output`/`ErrorOutput`/`DurationMs` |
| `grafana_query` | `GrafanaQueryPlugin` | `BaseUrl`(必填，支持子路径)/`Token` 或 `Username`+`Password`/`TimeoutSeconds`/`MaxListItems` | `Mode`(health/annotations/search)/`From`/`To`/`Tags`/`Query` | `Health`/`Annotations[]`/`Dashboards[]`/`Truncated` |
| `sqlserver_query` | `SqlServerQueryPlugin` | `Host`(必填)/`Port`(默认 1433)/`Database`/`Username`(必填)/`Password`/`TrustServerCertificate`(默认 true)/`MaxRows`/`CommandTimeoutSeconds` | `Sql`(必填，单条只读) | `Columns[]`/`Rows[]`/`RowCount`/`Truncated` |
| `alertmanager_query` | `AlertmanagerQueryPlugin` | `BaseUrl`(必填，支持子路径)/`Username`/`Password`/`BearerToken`/`TimeoutSeconds`/`MaxListItems` | `Mode`(alerts/silences/status)/`State`/`SilencesState` | `Alerts[]`/`Silences[]`（正则匹配器 ~ 前缀）/`Status`/`Truncated` |
| `loki_query` | `LokiQueryPlugin` | `BaseUrl`(必填，支持子路径)/`Username`/`Password`/`BearerToken`/`TimeoutSeconds`/`MaxLines`(1-2000)/`MaxLineChars`/`MaxListItems` | `Mode`(query/query_range/labels/label_values/series)/`Query`/`Time`/`Start`/`End`/`Direction`/`Label`/`Selector` | `Streams[]`（流标签+日志行）/`Labels[]`/`LabelValues[]`/`Series[]`/`Truncated` |
| `kubernetes_query` | `KubernetesQueryPlugin` | `BaseUrl`(必填)/`Token`(Bearer)/`SkipTlsVerify`(默认 true)/`TimeoutSeconds`/`MaxListItems`/`MaxLogChars` | `Mode`(pods/pod_logs/events/deployments/nodes)/`Namespace`/`LabelSelector`/`Pod`/`Container`/`TailLines`/`Previous` | `Pods[]`/`LogText`/`Events[]`/`Deployments[]`/`Nodes[]`/`Truncated` |
| `dingtalk_webhook_text` | `DingTalkWebhookTextPlugin` | `WebhookKey`(必填，完整地址或裸 access_token)/`Secret`(加签可选) | `Text`(必填)/`AtMobiles`/`AtAll` | `Errcode`/`Errmsg`/`Text` |
| `wecom_webhook_text` | `WeixinWorkWebhookTextPlugin` | `WebhookKey`(必填，完整地址或裸 key) | `Text`(必填)/`AtMobiles`/`AtAll` | `Errcode`/`Errmsg`/`Text` |
| `clickhouse_query` | `ClickHouseQueryPlugin` | `BaseUrl`(必填，ClickHouse HTTP 8123)/`Username`/`Password`(Basic，建议只读账号)/`Database`/`TimeoutSeconds`(1-300)/`MaxRows`(1-1000，默认 100) | `Sql`(必填，单条只读：SHOW DATABASES/SHOW TABLES/DESCRIBE/SHOW CREATE/SELECT 均可) | `Columns[]`/`ColumnTypes[]`(FORMAT JSON meta)/`Rows[]`（列名→值）/`RowCount`/`Truncated` |
| `clickstack_query` | `ClickStackQueryPlugin` | `BaseUrl`(必填，API server 端口)/`ApiKey`(Personal API Access Key)/`TimeoutSeconds`/`MaxRows`(1-500，默认 100) | `Mode`(sources/search/chart)/`SourceId`/`Where`/`WhereLanguage`(lucene/sql)/`Select`/`OrderBy`/`StartTime`/`EndTime`/`MaxResults`(≤2000)/`Offset`(≤10000)/`Granularity`/`AggFn`/`Field`/`GroupBy` | `Sources[]`/`Rows[]`（列名→值）/`RowCount`/`Truncated`/`Points[]` |
| `jina_search` | `JinaSearchPlugin` | `ApiKey`（必填，`InitAsync` 校验） | `Query`(必填)/`ReturnFormat`/`NoCache` | `Query`/`Results[]`（Title/Url/Description/Content）/`UsageTokens` |
| `jina_reader` | `JinaReaderPlugin` | `ApiKey`（必填，`InitAsync` 校验） | `Url`(必填，http/https)/`ReturnFormat`/`WithLinksSummary`/`WithImagesSummary`/`NoCache`/`Timeout` | `Url`/`Title`/`Description`/`Content`/`PublishTime`/`Links`/`Images`/`UsageTokens` |
| `yahoo_finance` | `YahooFinancePlugin` | `BaseUrl`（可选，留空用官方地址，可指向镜像/桩服务）/`TimeoutSeconds`(1-120，默认 20)/`MaxSymbols`(1-50，默认 10)/`MaxNews`(1-50，默认 10)/`MaxSearchResults`(1-50，默认 10)/`Lang`/`Region`（默认语言与市场区域，留空不传） | `Mode`(quote/news/search)/`Symbols`(quote 必填，逗号分隔)/`Query`(news/search 必填)/`NewsCount`/`SearchCount`/`Range`/`Interval`（IncludeHistory=true 时生效，白名单校验）/`IncludeHistory`/`Lang`/`Region`（请求级覆盖） | `Mode`/`Quotes[]`（报价+可选走势）/`News[]`/`Candidates[]`/`Errors[]`（单代码失败隔离）/`Truncated` |
| `smtp_email_send` | `SmtpEmailSendPlugin` | `Host`(必填，`InitAsync` 校验)/`Port`(1-65535，默认 465)/`Security`(ssl 默认/starttls/none)/`Username`(必填)/`Password`(必填，授权码)/`FromAddress`(可选，留空取 Username)/`FromName`/`TimeoutSeconds`(1-120，默认 15)/`MaxAttachmentMb`(1-50，默认 10) | `To`(必填，逗号/分号分隔)/`Cc`/`Bcc`/`Subject`(必填)/`Body`(必填)/`IsHtml`/`ReplyTo`/`Attachments[]`({FileName,ContentBase64}，≤10 个) | `MessageId`/`From`/`To[]`/`Cc[]`/`Bcc[]`/`Subject`/`AttachmentCount`/`AttachmentTotalBytes` |
| `imap_email_query` | `ImapEmailQueryPlugin` | `Host`(必填)/`Port`(默认 993)/`Security`(同上)/`Username`(必填)/`Password`(必填)/`Folder`(默认 INBOX)/`TimeoutSeconds`(默认 15)/`MaxBodyChars`(0-20000，默认 4000) | `Mode`(recent 默认/search/detail/mark_read/folders)/`Count`(1-50，默认 10)/`Folder`(请求级覆盖)/`FromContains`/`SubjectContains`/`SinceDate`(yyyy-MM-dd)/`UnreadOnly`/`Uid`(detail/mark_read 必填)/`Unread` | `ResultType`/`Folder`/`TotalCount`/`Messages[]`（信封摘要）/`Message`（detail 全文+附件元信息）/`Folders[]`/`UpdatedUid`/`Unread`/`Truncated` |
| `metaso_search` | `MetasoSearchPlugin` | `ApiKey`（必填，`InitAsync` 校验） | `Query`(必填)/`Scope`(webpage 默认/document/paper/image/video/podcast)/`Size`(1-100，默认 10)/`IncludeSummary`/`IncludeRawContent`/`ConciseSnippet` | `Query`/`Scope`/`Results[]`（Title/Link/Snippet/Date/Score/Position/RawContent，网页/文库/视频统一）/`Images[]`（Title/Link/Thumbnail）/`Total`/`Summary`/`Credits` |
| `metaso_reader` | `MetasoReaderPlugin` | `ApiKey`（必填，`InitAsync` 校验） | `Url`(必填，http/https)/`Format`(markdown 默认/json) | `Url`/`Title`/`Author`/`Date`/`Content`/`Credits`（Title/Author/Date/Credits 仅 json 形态） |
| `news_hot_board` | `HotBoardPlugin` | `Limit`(1-50，默认 20，每来源条数上限)/`Gateway`（可选，调试网关：设置后请求改写为 `{Gateway}/https/{域名}/{路径}`，留空直连官方） | `Sources`(必填数组，可多选：36kr/zhihu/weibo/juejin/toutiao/baidu/bilibili/douyin/ithome/hupu/huxiu/thepaper/geekpark/qq-news/netease-news/sina-news，接受中文别名) | `NewsList[]`（Source/SourceName/Rank/Title/Url/Desc/Cover/HotValue）/`Errors[]`（单来源失败隔离） |
| `base64_decode` | `Base64DecodePlugin` | `MaxDecodeBytes`(1024-33554432，默认 2MB，超出拒绝)/`TextMaxLength`(100-200000，默认 20000，超出截断) | `Base64`(必填，允许空白换行与 data URL 前缀)/`FileName`(可选，仅推断转存扩展名与结果回显) | `Kind`(text/image/binary)/`ContentType`/`SizeBytes`/`Text`(text 形态)/`TextTruncated`/`Url`(image/binary 形态)/`Markdown`(可直接引用)/`FileName` |

`bocha_web_search` 细节：

1. **外部调用复用基础设施层客户端**：`MoAI.AIPlugin.Dynamic` 引用 `MoAI.Infra.ExternalHttp`，构造注入 `IBoChaClient`（Refit + `ExternalHttpMessageHandler`，BaseAddress `https://api.bocha.cn`）；插件内不新建 `HttpClient`。顺带微调 infra：`WebSearchRequest.Include/Exclude` 改为可空（`DefaultIgnoreCondition=WhenWritingNull` 下不发空串）、`IBoChaClient.HandleApiError` 各分支补 `StatusCode`（符合「BusinessException 必须显式设 StatusCode」）。
2. **鉴权**：`ApiKey` 存裸 Key（`sk-xxx`），调用时拼 `Bearer {key}`；传 `Authorization` 头前会做 `Bearer ` 前缀幂等处理。
3. **参数兜底**：`Count` 用 `Math.Clamp(1,50)`；`Freshness` 空值回落 `noLimit`；`Query` 为空抛 `BusinessException(400)`。
4. **错误归一**：Refit 对非 2xx 抛 `ApiException`，插件转成 `BusinessException((int)StatusCode, "博查 Web Search 调用失败（HTTP xxx）：{body}")`，最终由 `PluginExecutor` 归一为 `Success=false` 展示；HTTP 200 但响应体 `code != 200` 由 `IBoChaClient.HandleApiError` 兜底。
5. **响应裁剪**：只返回网页与图片的常用字段（标题/链接/摘要/站点/图标/发布时间、缩略图/原图/宽高），丢弃 `cachedPageUrl`、`language`、`isFamilyFriendly` 等调试字段；`videos` 当前 API 恒为空，不纳入响应模型。

`bocha_ai_search` 细节：

1. **复用同一基础设施层客户端**：同样构造注入 `IBoChaClient`（走 `/v1/ai-search`），不新建 `HttpClient`；`Bearer` 头拼装与全网搜索共用 `BoChaAuthorization.Build`（`MoAI.AIPlugin.Dynamic/BoChaAuthorization.cs`，幂等追加前缀）。
2. **固定非流式**：插件引擎是「一问一答」（`IPluginRuntime.RunAsync` 返回 `Task<TResponse>`），无法把 SSE 增量透出给调用方，因此请求恒为 `Stream=false`；`Answer=true` 时博查在服务端完成大模型生成后一次性返回 `messages`，等价拿到同样内容。
3. **响应按 `messages` 重组**：`content` 是 JSON 文本，按 `content_type` 分派——
   - `type=answer` → 汇总进 `Answer`（Markdown）；`type=follow_up` → 追加进 `FollowUps`；
   - `content_type=webpage|image` → 展开为 `WebPages[]` / `Images[]`（字段与 `bocha_web_search` 共用 `BoChaWebPage`/`BoChaWebImage`）；
   - 其余 `content_type`（`weather_china_v2`、`baike_pro_v2`、`stock_v2`、`douyin` 等）→ 归入 `ModelCards[]`，`Type` 即 `content_type`，通用字段取 `name/url/snippet/summary/siteName/siteIcon/datePublished`（缺 `snippet` 时回落 `description`）；通用字段全空的条目不产出。
   - 结构展开兼容两种形态：非流式 `{"value":[...]}` 包装、流式裸对象/裸数组；空对象（当前 `video` 恒为 `{}`）不产出条目。
4. **参数兜底**：`Count` 用 `Math.Clamp(1,50)`；`Freshness` 空白回落 `noLimit`；`Query` 为空白抛 `BusinessException(400)`；`Answer`/`Include` 原样透传。
5. **错误归一**：与全网搜索一致——Refit 非 2xx → `BusinessException((int)StatusCode, "博查 AI Search 调用失败（HTTP xxx）：{body}")`；HTTP 200 但响应体 `code != 200` 由 `IBoChaClient.HandleApiError` 兜底。
6. **上游地址可配置**：`InfraExternalHttpModule` 读取 `MoAI:BoCha:Endpoint`（默认 `https://api.bocha.cn`），便于指向代理或本地桩服务；桩服务脚本据此实现无 Key 的端到端验证（见 [tdd.md](./tdd.md)）。

`moji_weather` 细节：

1. **复用基础设施层客户端**：构造注入 `IMojiWeatherClient`（`MoAI.Infra.MojiWeather`，Refit + `ExternalHttpMessageHandler`），调用 `POST {MoAI:MojiWeather:Endpoint}/whapi/json/aliweather/broadcast`（默认 `https://moji.market.alicloudapi.com`，阿里云云市场墨迹天气网关），请求为表单编码；插件内不新建 `HttpClient`。
2. **APPCODE 鉴权**：`AppCode` 存裸值，调用时经 `MojiWeatherAuthorization.Build`（`MoAI.AIPlugin.Dynamic/MojiWeatherAuthorization.cs`，幂等追加前缀）拼成 `APPCODE {AppCode}` 放 `Authorization` 头（云市场 APPCODE 简单认证）；部分服务规格要求的访问令牌 `Token` 非空时随表单下发，未配置不发该字段。
3. **定位参数二选一**：`CityId` 或 `Lat`+`Lon`（成对），两者皆缺失或只给其一抛 `BusinessException(400)`；取值先 `Trim` 再下发，且 `CityId` 优先于经纬度。
4. **响应容错解析**：上游返回 `{code,msg,data}` 信封——`code != 0` → `BusinessException(502, "墨迹天气返回错误（code=…）：{msg}")`；`data.city/condition/forecast` 用 `JsonDocument` 逐层容错展开（字段值兼容字符串/数字；`forecast` 兼容裸数组与 `{daily:[…]}`/`{value:[…]}` 包装；取不到任何有效字段的条目/对象直接丢弃），映射为裁剪后的 `City`（cityId/name/pname/counname）、`Condition`（temp/text/humidity/windDir/windLevel/windSpeed/pressure/icon/upDateTime）、`Forecast[]`（date/week/conditionDay~Night/tempDay~Night/wind*Day~Night/sunRise/sunSet）。
5. **错误归一**：Refit 非 2xx → `BusinessException((int)StatusCode, "墨迹天气调用失败（HTTP xxx）：{body}")`（401 AppCode 无效、403 未购买/欠费、429 限流）；响应非合法 JSON → `BusinessException(502)`；两路径统一由 `PluginExecutor` 归一为 `Success=false`。
6. **上游地址可配置**：`InfraExternalHttpModule` 读取 `MoAI:MojiWeather:Endpoint`，便于指向代理或本地桩服务；桩服务脚本据此实现无 AppCode 的端到端验证（`local-dev/moji-weather-e2e.mjs`，@DYN-S43~S47）。

`feishu_webhook_text` 细节：

1. **复用基础设施层客户端**：`MoAI.AIPlugin.Dynamic` 引用 `MoAI.Infra.ExternalHttp`，构造注入 `IFeishuWebHookClient`（Refit + `ExternalHttpMessageHandler`，路由模板 `bot/v2/hook/{key}`，BaseAddress `https://open.feishu.cn`）；不在插件内新建 `HttpClient`。**接口定义里 `key` 必须是 path 参数**（飞书 webhook 协议要求 token 出现在路径里），不要写成 `[Query] string key`——后者会让 Refit 把它附加成 `?key=xxx` 查询串，飞书会按 URL 错误处理。同时移除接口上的 `HttpClient Client { get; }` 暴露，避免诱导调用方自拼 URL。
2. **Webhook 输入归一**：配置可粘完整地址（含 `https://open.feishu.cn/open-apis/bot/v2/hook/{token}` 或仅 `{token}`），由 `NormalizeWebhookKey` 统一截取最后一个 `/hook/` 之后的 token，并切掉 `?`、`#`、尾随 `/`，避免用户粘贴查询串/路径斜杠导致飞书返回 `19001`。
3. **签名校验**：开启时填 `SignKey`，调用前给 `FeishuWebHookTextRequest` 补 `timestamp + sign`；未开启时保持为空，序列化时会被忽略。
4. **错误归一**：Refit 非 2xx → `BusinessException((int)StatusCode, "飞书推送失败（HTTP xxx）：{body}")`；HTTP 200 但响应 `code != 0` 按飞书文档码表映射——`19001` 400 地址无效、`19007` 400 机器人已禁用、`19021` 400 签名校验失败、`19022` 400 IP 不在白名单、`19024` 400 未含关键词、`9499` 429 触发限频、其余 `500`。`Success=false` 在 `PluginExecutor` 一并归一展示。
5. **参数兜底**：`Text` 为空抛 `BusinessException(400, "文本内容 Text 不能为空")`；`WebhookKey` 为空在 `InitAsync` 直接返可读错误，实例进入运行抽屉前即被拒。

`paddleocr_ocr` / `paddleocr_structure_v3` / `paddleocr_vl` 细节（三个模板共用基础设施与约定，差异见各小节）：

1. **复用基础设施层客户端**：构造注入 `IPaddleocrClient`（`MoAI.Infra.Paddleocr.IPaddleocrClient`，Refit + `ExternalHttpMessageHandler`），插件内不新建 `HttpClient`；三个模板共用同一客户端，分别调用 `OcrAsync`（`POST /ocr`）、`StructureV3Async`（`POST /layout-parsing`）、`PaddleOCRVLAsync`（`POST /layout-parsing`，靠请求体字段区分）。
2. **实例地址（`ApiUrl`）**：PaddleOCR 通常由用户在 AIStudio / 自部署里跑，因此 `ApiUrl` 走实例配置而非 `appsettings`；每次 `RunAsync` 把 `_client.Client.BaseAddress = new Uri(_config.ApiUrl)` 设到当前客户端——`IPaddleocrClient` 由 `AddRefitClient` 注册为 transient，每次插件执行由 DI 新建独立 `HttpClient`，并发运行互不干扰。
3. **鉴权头**：协议要求 `Authorization: token {Token}`（不是 OAuth `Bearer`），由 `MoAI.AIPlugin.Dynamic/PaddleOcrAuthorization.cs::Build` 统一处理：`Token` 非空时拼 `token {value}`，`Token` 为空时短路返回 `string.Empty`，避免给未开启鉴权的部署发送带尾随空格的 `Authorization` 头。
4. **错误归一**：Refit 非 2xx → `BusinessException((int)StatusCode, "PaddleOCR 调用失败（HTTP xxx）：{body}")`；HTTP 200 但响应体 `errorCode != 0` → `BusinessException(500, "PaddleOCR API 错误（{errorCode}）：{errorMsg}")`；两路径统一由 `PluginExecutor` 归一为 `Success=false`。
5. **参数兜底**：`File` 为空抛 `BusinessException(400, "文件 File 不能为空")`；`ApiUrl` 为空在 `InitAsync` 直接返可读错误，实例进入运行抽屉前即被拒。
6. **`prunedResult` 处理**：服务端把结构化结果塞在 `prunedResult`（`JsonElement`）里，插件按需展开——通用做法是 `value.GetRawText()` 回写原文 JSON 文本（`PrunedResultJson`），调用方自行解析；已知字段（`rec_texts`、`seal_res_list` 等）按需展开成强类型字段。
7. **响应 `dataJson` PascalCase**：插件结果由 `PluginExecutor` 用默认 `JsonSerializer.Serialize` 序列化，**字段名为 PascalCase**（`Pages/Text/OcrImage/InputImage/SealTexts/MarkdownText` 等），与 HTTP 接口的 camelCase 不一致；前端解析插件运行结果时按 PascalCase 读字段——属引擎既有行为。

`paddleocr_ocr` 差异：响应按 `Result.OcrResults[]` 聚合——每页 `Text` 由 `rec_textResult.PrunedResult.rec_texts` 数组按行拼接；`OcrImage`（标注识别框的可视化图）与 `InputImage`（PDF 时通常为 `null`）原样透传 Base64。

`paddleocr_structure_v3` 差异：响应按 `Result.LayoutParsingResults[]` 聚合——`PrunedResultJson` 保留 `prunedResult` 原文；`SealTexts` 仅在请求体 `useSealRecognition=true` 且服务端输出 `seal_res_list` 时填充，沿用旧 `NativePlugin.PaddleocrStructureV3Plugin.InvokeAsync` 的「取每条印章 `rec_texts[0]`」语义，但改为按印章拆为列表，方便上层逐条使用；`OutputImages`（按图像名索引）与 `InputImage` 原样透传。

`paddleocr_vl` 差异：响应按 `Result.LayoutParsingResults[]` 聚合——`MarkdownText` 取 `Markdown.Text`（视觉语言模型直接产出，可直接渲染）；`MarkdownImages` 取 `Markdown.Images`（相对路径 → Base64 字典）；强制开启 `Visualize=true`（与旧 `NativePlugin.PaddleocrVlPlugin.InvokeAsync` 行为一致，确保 `outputImages`/`inputImage` 字段被服务端产出）；其它 `PrettifyMarkdown`/`ShowFormulaNumber` 等开关按需透传。

> **新增 `local-dev/paddleocr-e2e.mjs`**：桩服务模式同 `bocha-search-e2e.mjs`——本地起一个监听 `/ocr` 与 `/layout-parsing` 的桩服务 → 拉起独立后端 → 创建三个模板实例并运行 → 断言「动态插件实例 → 注册表模板 → Refit → 桩服务 → 响应解析」整条链路（@DYN-S36~S42）。桩服务对未带 `Authorization: token ...` 的请求一律返回 401，便于覆盖鉴权头透传与上游错误归一两条分支；用请求体字段（`useLayoutDetection` / `useSealRecognition`）区分 VL 与 StructureV3。

`javascript_executor` 细节：
1. **沙箱执行**：插件内通过 **Jint 4.16.2**（中心化管理）执行用户填写的 JavaScript 代码——Jint 是纯托管 .NET 实现，无需 V8/Node 进程，便于随宿主分发；不在插件内 `new HttpClient`，无外部依赖。每次执行独立 `new Engine` 并 `using`，避免状态泄漏。
2. **资源限制（与旧 `NativePlugin.JavaScriptExecutorPlugin` 保持一致）**：Jint `Options` 同时启用 `LimitMemory(4_000_000)`、`LimitRecursion(100)`、`TimeoutInterval(TimeSpan.FromSeconds(4))`、`MaxStatements(1000)`；超限由 Jint 抛 `StatementsCountOverflowException` / `MemoryLimitExceededException` / `RecursionDepthOverflowException` / `TimeoutException`，均归一为 `BusinessException(400)` 由 `PluginExecutor` 转为运行结果失败。
3. **取消语义**：Jint 同步阻塞，把 `engine.Execute` 与 `engine.Invoke` 整体放进 `Task.Run`，外层 `await Task.Run(...).WaitAsync(cancellationToken)`，让调用方取消可协作；引擎对象在作用域结束后被 GC 回收。
4. **脚本约定**：用户填的 `JavaScriptCode` 必须导出 `function run(parameter)`（`parameter` 为字符串）；取函数引用**先用 `engine.Evaluate("typeof run === 'function'")` 探测**，缺失时直接返回可读提示 `JavaScript 代码必须定义 run(parameter) 函数`——若不探测而直接 `engine.Evaluate("run")`，Jint 会抛 `ReferenceError`，用户只能看到不可读的 `run is not defined`。确认存在后再取引用并以 `engine.Invoke(runFunction, parameters)` 调用；脚本语法错误 / 运行时错误归一为可读业务异常（400）。
5. **返回值归一**：用 Jint `JsValue.IsNull/IsUndefined/IsString/IsBoolean/IsNumber/IsArray/IsObject` 分派，对象/数组直接 `ToObject()` 后 `JsonSerializer.Serialize`，标量用 `JsonSerializer.Serialize` 字符串化（数字按整数或 `R` round-trip 保留精度），null/undefined `ResultJson=null`、对应 `ResultKind` 取自 `JsResultKind` 常量。
6. **响应契约**：返回 `Parameters`（回显入参）/ `ResultKind`（类型标记）/ `ResultJson`（与 `ResultKind` 配对的 JSON 文本）——调用方按 `ResultKind` 解析 `ResultJson`；与其它动态插件一致，`dataJson` 字段为 **PascalCase**（由 `PluginExecutor` 默认序列化约定决定，引擎既有行为，本轮未调整）。
7. **不持久化状态**：每次运行创建独立 DI 作用域（`PluginExecutor` 既定行为）并 `new Engine`，避免跨调用副作用；实例配置仅缓存 `JavaScriptCode` 字符串。

`postgres_query` / `mysql_query` 细节：

三层共同保证「只能执行只读 SQL」（实现：`SqlReadOnlyGuard` + 各插件的会话设置 + 资源上限）：

1. **文本层（前置校验，先于建立连接）**：`SqlReadOnlyGuard.Validate(sql)` 先剥离注释（`--`、`/* */`，PostgreSQL 支持嵌套）、字符串字面量（`''` 双写、反斜杠转义与 PostgreSQL 美元引用 `$tag$...$tag$`）、双引号/反引号标识符，再判定三件事——
   - **只允许单条语句**：剥离后出现「分号后仍有非空白字符」即拒绝（仅允许末尾单个分号）；
   - **首关键字白名单**：`SELECT`/`WITH`/`TABLE`/`VALUES`/`SHOW`/`EXPLAIN`/`DESCRIBE`/`DESC`；
   - **全句关键字黑名单**：DML（`INSERT`/`UPDATE`/`DELETE`/`MERGE`/`REPLACE`/`TRUNCATE`）、DDL（`DROP`/`CREATE`/`ALTER`/`RENAME`）、权限（`GRANT`/`REVOKE`）、写文件与建表（`INTO`/`OUTFILE`/`DUMPFILE`/`COPY`/`LOAD`）、过程与动态执行（`CALL`/`DO`/`EXECUTE`/`PREPARE`/`DEALLOCATE`）、会话与事务控制（`SET`/`RESET`/`BEGIN`/`COMMIT`/`ROLLBACK`/`SAVEPOINT`/`TRANSACTION`）、显式锁（`LOCK`/`UNLOCK`）、库维护（`VACUUM`/`REINDEX`/`CLUSTER`/`REFRESH`/`ANALYZE`/`DISCARD`/`FLUSH`/`OPTIMIZE`/`REPAIR`）。
     黑名单只收**不与常见列名/函数名冲突**的关键字（`END` 因 `CASE ... END` 被排除），确需同名标识符时用引号包裹即可通过（引号内容不参与扫描）。MySQL 的**可执行注释** `/*! ... */` 是例外：其内容会被服务端执行，因此保留参与扫描（而不是当普通注释丢弃）。
     拒绝时抛 `BusinessException(400, …)`，表现为「失败 + 只允许执行只读 SQL」；**校验先于连接**，故与目标库是否可达无关。
2. **连接层（服务端强约束）**：打开连接后立刻把会话设为只读——PostgreSQL 用 `SELECT set_config('default_transaction_read_only','on',false)`（等价 `SET SESSION CHARACTERISTICS AS TRANSACTION READ ONLY`），MySQL 用 `SET SESSION TRANSACTION READ ONLY`（MySQL 5.6.5+/MariaDB 10.0+）。此后写操作即使从文本层漏过（典型：以 `SELECT` 形态调用有副作用的函数），服务端也会以 `25006`（PG）/`1792`（MySQL）拒绝。**这层是真正的保证，文本层只是体验与纵深防御。**
3. **资源层**：`MaxRows`（1-1000，默认 100）在读取时截断并把 `Truncated` 置真，避免把整表拉进内存；PostgreSQL 另设服务端 `statement_timeout` + 客户端 `CommandTimeout`，MySQL 用客户端 `CommandTimeout`（驱动会终止超时查询）。

其它实现要点：

1. **会话设置语句不含用户输入**：为固定字面量 + 参数（PG 走 `set_config` 参数化），既不触发 CA2100 也无注入面；用户 SQL 本身是插件入参，用 `#pragma warning disable CA2100` 显式标注理由（与旧 `NativePlugin` 做法一致）。
2. **错误归一**：连接失败 → `BusinessException(400, "数据库连接失败：{驱动消息}")`；执行失败（语法/权限/超时/只读拒绝）→ `BusinessException(400, "PostgreSQL 执行失败：…" / "MySQL 执行失败：…")`，由 `PluginExecutor` 统一归一为 `Success=false`。
3. **结果形状**：`Columns`（按查询返回顺序）+ `Rows`（每行「列名 → 值」字典）+ `RowCount` + `Truncated`。**同名列**自动追加 `_2`/`_3` 后缀（否则字典键会相互覆盖）；`DBNull` 归一为 `null`；二进制列（`bytea`/`blob`）转 Base64 文本。取值为驱动原生类型（long/decimal/string/Guid/DateTime/DateTimeOffset/TimeSpan/数组等），由 `PluginExecutor` 默认 `JsonSerializerOptions` 序列化，故 `dataJson` 字段名为 **PascalCase**（与其它动态插件一致）。
4. **连接参数只来自实例配置**（面向用户自有的业务库，不是宿主库）：配置为离散字段 `Host`/`Port`/`Database`/`Username`/`Password`（与 ClickHouse/Prometheus 等模板一致的友好形态，用户不再手写连接串），`InitAsync` 校验 Host/Username 非空与 Port 取值（1-65535）后，经驱动 `MySqlConnectionStringBuilder` / `NpgsqlConnectionStringBuilder` 拼成连接串缓存（密码含 `;`、引号等特殊字符由 builder 自动转义），不打印、不落日志；配置示例与 `[Description]` 均提示「建议为插件单独创建只读账号」。
5. **无状态**：每次运行由 `PluginExecutor` 创建独立 DI 作用域与实例，仅缓存 `InitAsync` clamp 后的配置；连接用后即释放（驱动连接池按连接串隔离），会话只读设置在每次运行时重新下发。
6. **两个模板各自持有模型**：与 `bocha_*` 系列一致，分别使用 `PostgresQuery*` / `MysqlQuery*`（便于后续按数据库差异扩展）；共用的是 `SqlReadOnlyGuard`、`SqlResultReader`、`SqlQueryResult` 三个内部辅助类型（`MoAI.AIPlugin.Dynamic/` 根目录，与 `BoChaAuthorization` 同级）。
7. **驱动与版本**：PostgreSQL 用 `Npgsql` 10.0.3、MySQL 用 `MySqlConnector` 2.5.0，版本均由 `Directory.Packages.props` 集中管理，插件项目只加 `PackageReference`（新增包引用后需重新 `dotnet restore`）。

`http_probe` / `zabbix_query` / `redis_query` 细节（P1 智能运维三模板，成功路径证据 `local-dev/ops-p1-plugins-e2e.mjs`，@DYN-S50~S52）：

1. **拨测失败是结论不是异常**（http_probe）：不可达（连接拒绝/超时/解析失败/内网拒绝）一律 `Ok=false + Error` 随运行结果返回，仅参数非法抛 `BusinessException(400)`——AI 可把「不通」当诊断结论继续推理。内网防护 `HttpProbeGuard.IsPrivateAddress`（回环/RFC1918/链路本地/CGNAT 100.64/10/未指定地址/IPv6 ULA 与链路本地，IPv4-mapped 先展开）默认拒绝并把目标解析到的内网 IP 写进错误信息，`AllowPrivateNetwork=true` 显式放行（SSRF 护栏，护栏语义在 `[Description]` 中说明）。http 模式用 `IHttpClientFactory` 具名客户端 + 逐次 linked CTS 超时；正文预览有界读取（`BodyPreviewChars*4` 字节封顶）且失败不影响结论；tcp 的 RemoteAddress 对 IPv4-mapped 归一。
2. **Zabbix 端点拼装不用 Refit**（zabbix_query）：Refit 方法路径必须以 `/` 开头，与 BaseAddress 子路径互斥（`/api_jsonrpc.php` 会把 `http://host/zabbix/` 打回根路径）——`IZabbixClient` 改为 `IHttpClientFactory` 具名客户端（挂统一 `ExternalHttpMessageHandler`）+ 手工 `new Uri(new Uri(BaseUrl), "api_jsonrpc.php")` 拼端点，子路径部署（`http://host/zabbix`）得以支持；非 2xx 由 `ZabbixRpcClient` 归一为带状态码的 `HttpRequestException`。鉴权双形态由 `UseHeaderAuth` 切换：`Authorization: Bearer`（Zabbix 6.4+ 推荐）或 JSON-RPC `auth` 属性（5.x-7.x 通用）；API 令牌优先，否则 `user.login` 换会话（`username` 参数名失败回退 `user`，覆盖 5.0-7.x 的参数改名）；凭证只在运行态校验（保存实例不跑 `InitAsync`）。problems 的 `objectid` 即触发器 ID，经 `trigger.get` 二次查询富化主机名；列表 `limit=MaxListItems+1` 用于截断判定；`clock`（Unix 秒）转 ISO 并算人读时长，`severity` 字符串/数字宽容解析（`ZabbixResponseParser` 纯函数可单测）。
3. **Redis 传输层自研 RESP2**（redis_query）：弃用 StackExchange.Redis——其 3.x 握手（`HELLO 3`、`CLIENT SETINFO`、`CONFIG GET` 能力探针、`ECHO` 二进制 token 校验）与后台心跳对「每次运行新建连接」的诊断场景过重，且对自建桩不可对齐（`ScriptResultProcessor InternalFailure`）；`RedisRespClient` 只实现命令帧写入与五种回复解码（简单/批量字符串、整数、数组、nil，错误回复转 `BusinessException` 带命令名），`AUTH`/`SELECT` 仅在有配置时下发，用完即弃。只读保证：不接收自由命令，仅按模式下发白名单诊断命令（INFO/DBSIZE/SLOWLOG GET/CLIENT LIST/CONFIG GET/TYPE/TTL/STRLEN/HLEN/LLEN/SCARD/ZCARD/XLEN/MEMORY USAGE）；`MEMORY USAGE` 失败留空兼容老版本。INFO 文本分节、CLIENT LIST 行解析、SLOWLOG 条目组装为纯函数（`RedisResponseParser` 可单测）。
4. **实例模板零 UI 改动**：三个模板经 `[AiPlugin]` 反射自动进入注册表与 UI 模板下拉（配置示例填进 Monaco 编辑器），无需 syncapi/前端改动。

`ssh_executor` / `grafana_query` / `sqlserver_query` 细节（P2 运维三模板，证据 `local-dev/ops-p2-plugins-e2e.mjs`，@DYN-S54~S56）：

1. **SSH 处置护栏 = 「写操作白名单」规范的首次落地**（ssh_executor）：`SshCommandGuard` 校验先于建立连接，四层按序——①白名单 fail-closed（实例配置 `CommandWhitelist` 逗号分隔命令前缀，为空拒绝一切）；②拼接/替换符一律拒绝（`; && || &` 反引号 `$( 换行`，防止把任意命令拼到白名单命令之后）；③管道 `|` 放行但**每一段**分别校验前缀；④灾难级兜底（首 token mkfs/dd/shutdown/reboot/halt/poweroff/fdisk/parted/init，及「rm 带递归标志且目标为 / 或 /*」的灾难级递归删除）**白名单亦不可放行**。鉴权密码或 PEM/OpenSSH 私钥（私钥优先，`PrivateKeyFile` 无口令格式）；命令超时 `SshCommand.CommandTimeout`，输出按 `MaxOutputChars` 截断。SSH.NET 2026 的 `ConnectionInfo(host, port, user, methods)` 构造签名已变——直接用长期稳定的 `SshClient(host, port, user, password|PrivateKeyFile)` 便捷构造 + `client.ConnectionInfo.Timeout`。
2. **grafana_query 复用 factory+手工 Uri 模式**：`IGrafanaClient`（`GrafanaClient`，IHttpClientFactory 具名客户端挂统一 handler）按实例 BaseUrl（支持子路径）拼 `api/health`、`api/annotations`、`api/search`；鉴权 `OpsAuthorization`（Token→Bearer 优先，或 Basic）；注解时间参数接受 RFC3339/毫秒并归一为毫秒下发，Tags 逗号拆分逐个重复下发；响应毫秒时间戳转 ISO。health 在真实 Grafana 是匿名端点，401 归一用 annotations/search 验证。
3. **sqlserver_query 与 PostgreSQL/MySQL 的只读模型差异**：SQL Server 无会话级只读等价物，三层模型中的「连接层」由**账号层**替代——文本层 `SqlReadOnlyGuard`（复用，校验先于连接）+ 提示使用只读账号（db_datareader）；连接串经 `SqlConnectionStringBuilder`（自签名证书内网库默认 `TrustServerCertificate=true` 可关），结果复用 `SqlResultReader`（重名列/DBNull/二进制归一口径一致）。
4. **驱动版本**：SSH.NET 2026.0.0、Microsoft.Data.SqlClient 7.1.0，均由 `Directory.Packages.props` 集中管理。

`alertmanager_query` / `loki_query` / `kubernetes_query` / `dingtalk_webhook_text` / `wecom_webhook_text` 细节（P0 运维五模板，证据 `local-dev/ops-p0-plugins-e2e.mjs`，@DYN-S58~S62）：

1. **AM/Loki 复用 factory+手工 Uri 模式**（同 Zabbix/Grafana）：`IAlertmanagerClient` / `ILokiClient` 按实例 BaseUrl（支持子路径）拼 `api/v2/*` / `api/v1/*` 端点，鉴权 `OpsAuthorization`（Bearer 优先/Basic）。Alertmanager silences 的匹配器归一为「名=值」，正则匹配器值带 `~` 前缀；Loki 时间参数经 `LokiTimeHelper` 归一为 Unix 纳秒（RFC3339/秒/毫秒原文三态兼容），行数上限下发 `limit` 并按流逐行客户端再截断。
2. **kubernetes_query 的 TLS 按请求生效**：API Server 证书几乎总是私有 CA，`SkipTlsVerify` 无法用共享具名客户端承载——`KubernetesClient` 按调用注入 transient `ExternalHttpMessageHandler`（保留统一外部请求日志与遥测）并在其下挂按次创建的 `HttpClientHandler`（`disposeHandler: false` 防止 double-dispose）。鉴权 Bearer 令牌；非 2xx 的 401/403 翻译成「令牌无效/权限不足」可读提示；列表 `limit` 服务端+客户端双重截断（不翻页）；`pod_logs` 是纯文本端点（非 JSON）直读透传。
3. **钉钉/企微机器人与飞书同构**：`WebhookKey` 归一化（完整地址截取 `access_token=` / `key=` 之后，或裸值直用）；报文模型复用基础设施层（`MoAI.Infra.DingTalk.Models` / `MoAI.Infra.WeixinWork.Models`）；客户端走 Refit 固定域名 + `MoAI:DingTalk:RobotEndpoint` / `MoAI:WeixinWork:RobotEndpoint` 配置覆盖（E2E 桩注入）。钉钉「加签」安全设置经 `DingTalkSignHelper`（HMAC-SHA256(secret, "{ms}\n{secret}")→base64→URL 编码，timestamp/sign 放查询串）；企微无加签（IP 白名单），文本按 2048 字节截断（mentioned_list `@all` / mentioned_mobile_list）。
4. **业务错误兜底**：钉钉/企微把关键词未命中/签名失败/token 无效等业务错误也放 HTTP 200，按 `errcode!=0` 兜底（310000 归一为「关键词/加签/白名单」设置提示）。

`clickhouse_query` 细节（ClickHouse 自由只读 SQL 模板，证据 `local-dev/observability-plugin-e2e.mjs` ClickHouse 段，@DYN-S71~S72；2026-10-08 改版）：

1. **自由 SQL 而非固定模板**：请求仅 `Sql` 一个参数，AI 自行完成「`SHOW DATABASES` 摸库 → `SHOW TABLES FROM 库名` 摸表 → `DESCRIBE TABLE` / `SHOW CREATE TABLE` / 查 `system.columns` 看结构 → `SELECT ... LIMIT n` 查询」全流程；工具 Description 与 `Sql` 参数 Description 内置该工作流指引。旧 traces/logs/metrics 三套便捷模板与 `OtelDatabase` 配置已删——固定过滤参数对 AI 过死板，模型自己写 SQL 更灵活。
2. **只读三层防护**：文本层 `SqlReadOnlyGuard` + `ClickHouseReadOnlyGuard`（拒写操作/DDL/会话与事务控制/多语句/SYSTEM 等服务器命令/url()、s3()、remote() 等外部源表函数）；连接层 HTTP 请求固定 `readonly=1` 服务端兜底（文本校验被绕过也写不进）；资源层 `max_execution_time` + `max_result_rows`(MaxRows+1) + `result_overflow_mode=break` + 客户端按行截断置 `Truncated`。守卫放行面：首关键字为 SHOW 的语句跳过全句关键字扫描——`SHOW CREATE TABLE/VIEW` 是摸结构最有用的语句，原扫描会因对象名必然携带的 CREATE 等词误杀（SHOW 全部为元数据读取；该放行对 mysql/postgres/sqlserver 守卫同样生效）。
3. **出参 FORMAT JSON**（自 JSONEachRow 切换）：列名/列类型取响应 `meta`（**空结果集也带列信息**，AI 摸结构时不会因零行丢失 schema），行取 `data` 并经 `ObservabilityJson.ToClr` 归一（Map/嵌套→对象、Int64 裸数字、NaN/Inf 文本）；用户 SQL 末尾 FORMAT 子句剥离（`StripTrailingFormatClause`），出参格式由请求 `default_format=JSON` 统一控制。错误归一：Refit `ApiException` → `BusinessException(状态码, "ClickHouse 调用失败（HTTP x）：正文")`，网络层 `HttpRequestException` → 502 可读失败。
4. **与 clickstack_query 分离**（用户澄清 2026-10-08）：ClickStack 部署里内置 ClickHouse 的 8123/9000 通常不发布到宿主机，HyperDX 对外 API 是其唯一查询面且无 SQL 直通端点，故 `clickstack_query`（sources/search/chart 三模式 + Personal API Access Key）原样保留，两模板在工具描述里互相指路——可直连的 ClickHouse 用本模板自由 SQL，ClickStack 观测数据用 `clickstack_query`。

`clickstack_query` 细节（ClickStack 观测查询模板，证据 `local-dev/clickstack-e2e.mjs`，@DYN-S64~S66）：

1. **查询面是 HyperDX 对外 API 而非 ClickHouse**：ClickStack 部署里 ClickHouse 的 8123/9000 通常不发布到宿主机（对外只有 OTLP 4317/4318 摄入口，不可查询），插件走镜像内置的独立 API server（默认 8000 端口，与 UI 8080 分离；BaseUrl 支持任意端口偏移与子路径），端点 `api/v2/sources`、`api/v2/search`、`api/v2/charts/series`。鉴权固定 `Authorization: Bearer` + **Personal API Access Key**（HyperDX UI → Team Settings → API Keys 创建，与 OTLP 摄入的 Ingestion Key 不同）；`IClickStackClient` 复用 factory+手工 Uri 模式（同 Zabbix/Grafana）。
2. **时间窗归一**：search 下发 ISO 秒精度（缺省 End=now、Start=End-15 分钟，与服务端缺省一致）；chart 契约要求 epoch 毫秒且时间必填（插件缺省 End=now、Start=End-1 小时）。时间解析无时区按 UTC 解释（`ClickStackResponseParser.TryParseTime` 纯函数可单测）。
3. **行数与截断**：`MaxResults`（≤服务端硬上限 2000）缺省取配置 `MaxRows`(1-500)；截断语义为「返回行数达到上限即 Truncated=true」（服务端 rows 只给本次行数无总数），用 `Offset`(≤10000) 翻页。where/select/orderBy 长度上限与服务端对齐（8192/4096/1024），超限提前 400。
4. **错误归一与边界**：非 2xx 按状态给可读提示——401/403 提示「ApiKey 需为 Personal API Access Key（非 Ingestion Key）」，404 提示检查 SourceId 与 BaseUrl；search 错误形态 `{message}`、chart 形态 `{error}` 均可提取（`ExtractErrorMessage`）。凭证与 BaseUrl 校验都在运行态（保存实例不跑 `InitAsync`，同 Zabbix 口径）；不加网络守卫（BaseUrl 由管理员配置，同 zabbix/grafana 口径）。
5. **Search 端点有镜像版本要求**（2026-09-29 真机实测）：`POST /api/v2/search` 是对外 API server 较新构建才有的端点——2026-07 前后的 `hyperdx:2` 镜像里 `GET /api/v2/sources` 与 `POST /api/v2/charts/series` 均已存在（无 Key 时 401「Unauthorized」即路由存在的正常形态），但 `POST /api/v2/search` 返回 Express 的「Cannot POST」HTML 404，升级 `clickstack-app` 镜像即可获得；插件对该形态给「端点不存在（可能版本过旧或 BaseUrl 指向 UI）」专属提示，与 SourceId 无效的 404 区分。

`jina_search` / `jina_reader` 细节（Jina AI 搜索与网页阅读模板，证据 `local-dev/jina-search-e2e.mjs`，@DYN-S76~S78）：

1. **两模板分立对接 Jina 两条产品线**：`jina_search` 走 Search API（`https://s.jina.ai`，`POST /`，请求体 `{q}`），`jina_reader` 走 Reader API（`https://r.jina.ai`，`POST /`，请求体 `{url}`）；两者都是一次 HTTP 往返返回完整 JSON（`{code,status,data,...}` 信封，搜索 `data` 为结果数组、阅读为单对象），插件引擎无需透出 SSE。构造注入 `IJinaSearchClient`/`IJinaReaderClient`（Refit + `ExternalHttpMessageHandler`），插件内不新建 `HttpClient`。
2. **鉴权**：`ApiKey` 存裸 Key（`jina_xxx`），调用时经 `JinaAuthorization.Build`（`MoAI.AIPlugin.Dynamic/JinaAuthorization.cs`，与博查同款幂等追加 `Bearer ` 前缀）放 `Authorization` 头；阅读请求固定 `Accept: application/json` 取结构化响应。
3. **可选能力走 X- 头而非请求体**：正文格式 `X-Return-Format`（markdown/text/html/screenshot/pageshot，空白或非法回落 markdown）、`X-No-Cache`、阅读专属的 `X-With-Links-Summary`/`X-With-Images-Summary`/`X-Timeout`（秒）；开关为 false 或超时未配置时**不发头**（Refit 参数传 null 即省略），避免向服务端发送冗余头。
4. **参数校验**：`Query`/`Url` 空白抛 `BusinessException(400)`；阅读的 `Url` 还须经 `Uri.TryCreate(Absolute)` 校验为 http/https 完整地址；`UsageTokens` 为各条结果 `usage.tokens` 求和（阅读取单值）。
5. **错误归一**：Refit 非 2xx → `BusinessException((int)StatusCode, "Jina xxx 调用失败（HTTP xxx）：{body}")`；HTTP 200 但信封 `code != 200`（额度耗尽 429 等）由 `JinaEnvelope<T>.EnsureSuccess` 兜底，错误信息取 `readable_message`/`message`。两路径统一由 `PluginExecutor` 归一为 `Success=false`。
6. **上游地址可配置**：`InfraExternalHttpModule` 读取 `MoAI:Jina:SearchEndpoint` / `MoAI:Jina:ReaderEndpoint`（默认官方地址），便于指向代理或本地桩服务；桩服务按请求体含 `q`/`url` 区分两个端点，实现无 Key 的端到端验证（见 [tdd.md](./tdd.md)）。
7. **infra 命名空间坑**：`MoAI.Infra.*` 命名空间下写全限定 `System.Collections.Generic.List<>` 会被解析成 `MoAI.Infra.System.Collections...`（CS0234，Refit 源生成器同样报错），须 `using System.Collections.Generic;` 后用简单类型名。

`yahoo_finance` 细节（雅虎财经模板，证据 `local-dev/yahoo-finance-e2e.mjs`，@DYN-S79~S84）：

1. **单模板三模式复用官方两个公开端点**：quote 逐代码调 `GET /v8/finance/chart/{symbol}`（meta 含最新价/昨收/日内高低/成交量/52 周高低，indicators 含区间采样点），news/search 共用 `GET /v1/finance/search`（按 `newsCount`/`quotesCount` 谁大于 0 区分取 news 数组还是 quotes 数组）；构造注入 `IYahooFinanceClient`（Refit + `ExternalHttpMessageHandler`），插件内不新建 `HttpClient`。无需任何 API Key。
2. **数据源地址在实例配置**：`BaseUrl` 可选（留空官方 `https://query1.finance.yahoo.com`），可指向镜像/代理/桩服务（同 zabbix/grafana 口径，不加网络守卫）；Refit 客户端不注册固定 BaseAddress，每次运行由插件 `PrepareClient` 按 `_config.BaseUrl` 设置（transient + 每次独立作用域，实例内只设一次）。因无应用级覆盖项，桩服务 E2E 全靠实例配置指向本地桩。
3. **浏览器 User-Agent 必带**：雅虎对非浏览器 UA 的请求常直接拒绝（地域/反爬），`PrepareClient` 固定追加 Chrome UA 与 `Accept: application/json`；国内部署直连大概率被拦，建议配置镜像地址。
4. **quote 批量失败隔离**：`Symbols` 按逗号/分号/空格切分去重，超过 `MaxSymbols` 截断并置 `Truncated`；逐代码调用，无效代码（上游 HTTP 200 + `chart.error`，如已退市）与 HTTP 非 2xx 都归入 `Errors[]`，其余代码照常返回，不拖垮整批。涨跌额/涨跌幅由 `regularMarketPrice - chartPreviousClose`（回落 `previousClose`）计算，百分比保留两位。
5. **走势序列可选**：`IncludeHistory=true` 才下发 `Range`/`Interval`（白名单校验，非法 400），从 `timestamp` 与 `indicators.quote[0].close` 按下标配对，空收盘剔除，最多保留最近 30 点（`Time` 为 UTC ISO 8601）。
6. **参数校验与覆盖**：`Mode` 白名单（quote/news/search，空白回落 quote）；quote 缺 `Symbols`、news/search 缺 `Query` 抛 `BusinessException(400)`；条数参数按实例配置上限 clamp（下限 1）；`Lang`/`Region` 请求级优先于实例默认，空则不下发。
7. **错误归一**：Refit 非 2xx → `BusinessException((int)StatusCode, "雅虎财经调用失败（HTTP xxx）：{body}")`；news/search 模式整体失败归一 `Success=false`；时间戳统一转 UTC ISO 8601（`providerPublishTime`/`regularMarketTime`）。

`smtp_email_send` / `imap_email_query` 细节（邮件双模板，证据 `local-dev/email-plugins-e2e.mjs`，@DYN-S85~S102）：

1. **协议栈 MailKit 4.18.1**（中心化版本管理）：SMTP 发信走 `MailKit.Net.Smtp.SmtpClient`、收件读取走 `MailKit.Net.Imap.ImapClient`，插件内不新建 HTTP 客户端；连接/认证/收发全部挂 `CancellationTokenSource.CancelAfter(TimeoutSeconds)` 超时护栏并同步设置 `client.Timeout`。安全模式由 `MailSecurityParser` 统一解析（空值回落 `ssl`，未知值 400）。
2. **收件人解析务实化**（`EmailRecipientParser`）：MimeKit 的 `MailboxAddress.Parse` 对裸词/缺域地址过宽（`not-an-email` 也能解析出 Address），解析后再做结构校验——恰好一个 `@`、本地部分与域名非空、域名至少一个点且无空白；多收件人按逗号/分号/换行拆分并忽略大小写去重。
3. **Message-ID 由插件预生成**（`<{Guid v7}@{发件域}>`）并回显，便于与收件侧联动核对；注意 MimeKit 的 `MimeMessage.MessageId` 属性**读写均不带尖括号**（报文头里才有），E2E 断言按去括号后的值比对。
4. **附件三重护栏**：数量 ≤10、单文件 ≤20MB、总大小 ≤ `MaxAttachmentMb`；`FileName` 剥离路径分隔符（`..\..\` 清洗后为空即拒绝）；Base64 解码失败 400。MimeKit 对纯 ASCII 内容自动选 7bit 传输编码（不强求 base64）。
5. **IMAP 列表只取信封摘要**（`MessageSummaryItems.Envelope|Flags|Size|InternalDate|UniqueId`，不含正文——避免把整箱邮件拉进上下文），detail 模式才 `GetMessage` 取单封全文并按 `MaxBodyChars` 截断；附件只回元信息（文件名/类型/大小）。截断语义：SEARCH 命中数超过 `Count` 时取最新 N 封并置 `Truncated=true`。
6. **全量用户输入校验先于网络连接**：未知 Mode、search 无过滤条件、detail/mark_read 缺 Uid、非法 `SinceDate` 均在 `ConnectAsync` 之前拒绝（不消耗 IMAP 会话）。
7. **SinceDate 语义**：IMAP 的 `SINCE` 含当天；MailKit 渲染 SINCE 日期取 UTC 部分，因此 `yyyy-MM-dd` 固定按 UTC 零点构造 `SearchQuery.DeliveredAfter`（`DateTimeOffset.TryParseExact` + `AdjustToUniversal`），保证服务端看到的日期与用户输入一致。
8. **错误归一**：`AuthenticationException` → 401「认证失败（部分服务商要求授权码并开启 IMAP/SMTP 服务）」；`FolderNotFoundException` → 400 提示用 folders 模式查看可用文件夹；`SslHandshakeException` → 502 提示 Security 与端口匹配；超时（链接 CTS 触发）→ 504；其余 → 502 带首行错误。均由 `PluginExecutor` 归一为 `Success=false`。
9. **无状态与凭证**：每次运行独立 DI 作用域 + 连接用后即弃；`Username/Password` 仅存实例配置（`plugin_dynamic.config`），凭证只在运行态校验（保存实例不跑 `InitAsync`，同 zabbix/grafana 口径）；`Security=none` 明文仅供内网调试（配置描述注明）。

`metaso_search` / `metaso_reader` 细节（秘塔搜索工具集模板，证据 `local-dev/metaso-search-e2e.mjs`，@DYN-S103~S105）：

1. **两模板共基址分端点**：搜索与阅读都在 `https://metaso.cn` 下的 `/api/v1/search` 与 `/api/v1/reader`，共用一个 `IMetasoClient`（Refit + `ExternalHttpMessageHandler`，构造注入，插件内不新建 `HttpClient`）；搜索响应为裸载荷（无 code 信封，按 scope 分 `webpages`/`documents`/`images`/`videos` 集合），错误以非 2xx HTTP + `{message}` 表达，故无需业务码兜底层。
2. **鉴权**：`ApiKey` 存裸 Key（`mk-xxx`，在 metaso.cn/search-api/api-keys 获取），调用时经 `MetasoAuthorization.Build`（与 Jina/博查同款幂等追加 `Bearer ` 前缀）放 `Authorization` 头；两模板各自独立配置（互不共享实例）。
3. **搜索结果统一列表**：网页/文库/视频结果字段同构，统一并入 `Results`（便于 AI 与流程编排按一个列表消费），仅 image 范围走 `Images`（标题/图片地址/缩略图地址）；`IncludeSummary=true` 时上游做摘要召回增强并返回综合摘要 `Summary`，`IncludeRawContent=true` 时每条网页结果带来源原文 `RawContent`，`ConciseSnippet=true` 时摘要为精简原文匹配信息。
4. **参数校验与钳制**：`Query` 空白抛 400；`Scope` 空白回落 `webpage`、大小写归一，非法值 400 列出合法范围；`Size` `Math.Clamp(1,100)`；阅读 `Url` 须过 `Uri.TryCreate(Absolute)` 且 http/https。
5. **阅读双形态**：`Format=markdown`（默认）走 `ReadMarkdownAsync`（`[Headers("Accept: text/plain")]`，Refit 对 `Task<string>` 返回响应体原文）；`Format=json` 走 `ReadJsonAsync`（Accept: application/json，解析标题/地址/作者/日期/正文/点数）；非法 Format 回落 markdown。响应正文优先取 `markdown` 字段、回落 `content`。
6. **上游地址可配置**：`InfraExternalHttpModule` 读取 `MoAI:Metaso:Endpoint`（默认官方地址 `https://metaso.cn`，与 BoCha 同款根基址 + 绝对路径写法——Refit `[Post]` 路径以 `/` 开头会吃掉基址子路径，因此默认基址不带 `/api/v1`），便于指向代理或本地桩服务；桩按请求体含 `q`/`url` 区分端点、按 Accept 头区分阅读双形态。
7. **错误归一**：Refit 非 2xx → `BusinessException((int)StatusCode, "秘塔xxx调用失败（HTTP xxx）：{body}")`，由 `PluginExecutor` 归一为 `Success=false`（401 密钥无效、402 点数不足、429 限流）。

`news_hot_board` 细节（新闻热榜聚合模板，证据 `local-dev/hot-board-e2e.mjs`，@DYN-S106~S116）：

1. **单模板聚合 16 渠道**：来源是数组参数而非多模板（AI 一次调用可拿多平台榜单）；渠道无 DI、由 `HotBoardCatalog` 查表构造（纯解析逻辑，单测直接 new + 桩 `HttpMessageHandler`）；渠道实现移植自姊妹项目（WoAI）已验证的 15 个渠道，掘金（`article_rank` GET，热度取阅读数）为本轮新实现。
2. **渠道基类统一横切面**：`HotBoardChannelBase` 按目标主机稳定选档补浏览器指纹头（UA/Sec-Ch-Ua/Sec-Fetch 等，上游网页接口缺 UA 会被风控）、连接失败与非 2xx 统一包装 502、非法 JSON 归一 502；`GetString/GetScalar/GetNested*` 容错读取（长整型 id 取原始文本防精度丢失）。
3. **网关改写收在基类**：配置 `Gateway` 后请求统一改写为 `{Gateway}/https/{域名}/{路径}`（`Url()` 一处实现，16 渠道零感知），供镜像/抓包/本地桩服务使用；命名 HttpClient `hot_board`（`DynamicPluginsModule` 注册：20s 超时、关闭自动 Cookie——抖音渠道手动携带 csrf token、挂 `ExternalHttpMessageHandler` 外部请求日志）。
4. **并发拉取 + 失败隔离**：来源并发 `Task.WhenAll`，单渠道异常归一进 `Errors`（渠道名 + 原因）不拖垮整轮（yahoo 失败隔离同款语义）；空结果也记为错误（「未返回任何条目（可能被风控或榜单结构变化）」）；全部失败才抛 502「全部来源抓取失败」。合并按请求顺序、来源内补排名 1..n、`Limit` 截断。
5. **异构上游解析**：JSON 接口（知乎/36氪/掘金/微博/头条/虎扑/网易/腾讯/新浪/澎湃/极客公园/B站/抖音）逐层容错展开；HTML/文本壳（百度 s-data 注释、IT之家 AngleSharp、虎嗅 br 分段、新浪 `var data =` 剥壳）各自专属解析；B站 WBI 签名（nav 取 key 进程内缓存 1h，主接口失败回落旧版 ranking）；抖音先取 passport Set-Cookie 再携 Cookie 拉榜。

`base64_decode` 细节（Base64 解码模板，证据 `tests/MoAI.AIPlugin.Dynamic.Tests/Base64DecodePluginTests.cs`，@DYN-S117~S120）：

1. **形态三分**（`Base64ContentDetector`）：严格 UTF-8 判定（解码失败或含 NUL 等控制字符即非文本）→ 文本直接回填 `Text`；图片魔数（PNG/JPEG/GIF/WebP/BMP/ICO/TIFF）与 SVG 文本特判（SVG 是文本但按图片处理才能在对话渲染）→ 转存 `public/images`；其余二进制 → 转存 `public/files`，`Kind` 标记 text/image/binary。
2. **图片/二进制不回 data URL**：前端 react-markdown 默认 `urlTransform` 会剥 `data:` URL，且大负载直接撑爆模型上下文——统一经 `IStorageService.UploadStreamAsync` 按 SHA-256 内容寻址转存（同内容幂等去重），响应回 `/static` 公开地址与 `Markdown` 引用（图片 `![image](url)`、其它 `[file](url)`）。这是动态插件首个构造注入 `IStorageService` 的模板（csproj 增 `MoAI.Storage.Shared` 引用）。
3. **输入宽容**：允许空白与换行（`Convert.FromBase64String` 语义）、自动剥离 `data:<mime>;base64,` 前缀；二进制形态有类型声明时信任声明定扩展名（application/pdf → .pdf 等白名单），无声明按 octet-stream/.bin；`FileName` 仅取扩展名参与 ObjectKey 与回显，不进地址。
4. **护栏**：解码前按「原始长度/4×3」粗估上界（原始长度 ≥ 有效字符数不会低估，杜绝超大输入先占内存）；解码后精确比对 `MaxDecodeBytes`（`InitAsync` 钳制 1024-32MB）；文本按 `TextMaxLength` 截断（100-200000）置 `TextTruncated`；空白/非法 Base64/超限均 `BusinessException(400)` 归一且不发生转存。

## 已知问题

- `templete_key` 列名拼写沿用用户给定的 `templete_key`（非 `template_key`）；如需修正属数据/DB 迁移事项。
- 动态模板的配置仍以 `configJson` 在 `PluginExecutor.InitAsync` 校验；此处实例 `config` 直接作为该值传入。
- **管理员侧「实例 key 与其它实例重复 → 409」为死代码**：`SaveDynamicPluginCommandHandler` 先按 `plugin_key` 查已有实例，命中即走更新；未命中时 `EnsureInstanceKeyUniqueAsync` 的 DB 判重谓词（`plugin_key == key && IsDeleted == 0`）与前者完全相同，恒为 false。跨团队冲突由团队插件链路负责（teamplugin TP-13），如需在管理员侧禁止重名需另行定义语义（例如要求 key 携带团队/命名空间）。
- 插件运行结果的 `dataJson` 使用 PascalCase 字段名（`PluginExecutor` 以默认 `JsonSerializerOptions` 序列化响应对象），与 HTTP 接口的 camelCase 风格不一致；消费插件结果时需按 PascalCase 读字段。属引擎既有行为，改动会影响所有既有模板，未在本轮调整。
- 博查两模板的**成功路径**已由 `local-dev/bocha-search-e2e.mjs` 用桩服务自动覆盖（@DYN-S16 / @DYN-S22）；桩服务返回的是官方文档样例报文，**真实上游字段若与文档有出入仍需一次人工走查**。
- `bocha_ai_search` 的模态卡只解析通用字段：`douyin` 等非通用结构（`cover_images`、`interactions` 等）会被裁剪，仅保留 `Type` 与可映射的 `description`。
- `moji_weather` 按阿里云云市场公开样例报文（`{code,msg,data}` 信封 + camelCase 字段）实现并容错解析；墨迹官方完整接口文档需注册后获取，真实上游字段若与样例有出入仍需一次人工走查（同博查既有口径）。
- Jina 两模板按官方公开文档样例报文（`{code,status,data}` 信封）实现；本机代理未启动时无法实测真实上游，**真实字段若有出入仍需一次人工走查**（同博查/墨迹口径）。
- `imap_email_query` 列表模式为信封级摘要（无正文/无附件标志），snippet 与附件列表只在 detail 模式提供；若后续需要「列表即摘要」，可改走 BODYSTRUCTURE+部分取（协议面更宽，暂不做）。
- `yahoo_finance` 按雅虎财经公开端点（v8 chart / v1 search）样例报文实现；本机直连雅虎被地域拦截、代理未启动时无法实测真实上游，**真实字段若有出入仍需一次人工走查**（同博查/墨迹口径）。国内部署直连官方地址大概率被拦，建议实例配置镜像地址。
- `news_hot_board` 渠道解析已按官方报文桩全量覆盖（@DYN-S106~S116），且 E2E 调试期间曾以真实上游全 16 渠道拉通（310 条真实数据解析正确）；但热榜接口均为网页端非公开接口，**平台风控或结构变化会导致单渠道失败**——已有失败隔离兜底（进 `Errors` 不影响其它来源），渠道修复属常规维护。
