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
│   ├── Commands/SaveDynamicPluginCommand.cs        {pluginKey,templeteKey,title,description,classifyId,config} → EmptyCommandResponse
│   ├── Commands/DeleteDynamicPluginCommand.cs       {pluginKey} → EmptyCommandResponse
│   └── Queries/Responses/QueryPluginManageListCommandResponseItem.cs  （增强：+TempleteKey,+Config,+ConfigExample）
├── MoAI.AIPlugin.Dynamic/                            内置模板宿主（Models/ 各模板模型 + Plugins/ 模板实现）
├── MoAI.AIPlugin.Core/
│   ├── Commands/RunPluginCommandHandler.cs          （增强：key 未命中注册表时走实例解析器）
│   └── Services/IDynamicInstanceResolver.cs         {Resolve(instanceKey) → (Template,ConfigJson)}
├── MoAI.AIPlugin.Custom/
│   ├── Commands/SaveDynamicPluginCommandHandler.cs  校验 + 创建/更新实例
│   ├── Commands/DeleteDynamicPluginCommandHandler.cs 软删除
│   ├── Services/DynamicInstanceResolver.cs          实例 key → 模板 key + 配置
│   ├── Queries/QueryPluginManageListCommandHandler.cs （增强：动态实例合并 + 模板字段填充）
│   └── CustomPluginModule.cs                        注册 IDynamicInstanceResolver
└── MoAI.AIPlugin.Api/
    └── Controllers/DynamicPluginController.cs       [Route("/ai/plugin/dynamic")]，门禁在 Controller

ui/src/
├── api/plugin.ts                                    +getDynamicTemplates,+saveDynamicPlugin,+deleteDynamicPlugin
└── pages/plugins/DynamicPluginPanel.tsx              实例列表 + 新建/编辑弹窗（Monaco 配置）+ 运行 + 删除
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
| GET | `/api/ai/plugin/manage/list` | admin | 动态实例列表（`kind=dynamic`），带 `templeteKey/config/configExample` |

> 控制器 `[Route("/ai/plugin/...")]` 之上还有全局 `/api` 路由前缀，实际路径以 `/api/ai/plugin/...` 为准（早期文档漏写 `/api`，2026-09-11 依 OpenAPI 实测修正）。

`QueryPluginManageListCommandResponseItem` 新增字段：
- `templeteKey`（string?）：动态实例的模板 key。
- `config`（string?）：动态实例存储的配置 JSON。
- `configExample`（string?）：动态模板配置示例（创建时 Monaco 初始值）。

## 关键决策

1. **实例 key 存 `plugin_dynamic.plugin_key`**；**模板 key 存 `plugin_dynamic.templete_key`**（`.NET` 字段 `TempleteKey`；用户新增该列）。`plugin` 行 `PluginName`=实例 key、`PluginId`→`plugin_dynamic.Id`、`Type=NativePlugin`、`IsSystem=true`。
2. **key 唯一性**：实例 key 全小写+下划线（`^[a-z_][a-z0-9_]*$`），≤30。`save` 是 **upsert**：同实例 key 再次提交按更新处理（不新建、不报冲突，见 @DYN-S5）；仅当实例 key 与**注册表 key**（静态/动态模板 key）重名时 409「实例 Key 已被使用」。
3. **模板校验**：`templeteKey` 必须在注册表且 `IsDynamic`，否则 404「动态插件模板不存在」。
4. **运行解析**：`RunPluginCommand` 的 `key` 先查注册表；未命中则用 `IDynamicInstanceResolver.Resolve(key)` 由 `plugin_dynamic.plugin_key`→`templete_key`→`registry.Get(templete_key)`，取该实例 `config` 初始化；`configJson` 无需前端传。
5. **编辑不可改实例 key**：更新只改 `templeteKey/config/title/description/classifyId`；实例 key 作为主键定位。
6. **分类校验**：`classifyId` 非 0 需在 `classify` 表存在且 `Type=plugin`，否则 400。
7. **删除**：软删除 `plugin_dynamic` 与该实例关联的 `plugin` 行（`IsDeleted=1`）。
8. **前端**：动态 Tab 用 `DynamicPluginPanel`；新建/编辑弹窗内含 Monaco 配置编辑器；运行复用 `PluginRunDrawer`（`paramsExample` 来自模板）。i18n zh/en 同步。

## 内置动态模板

模板 key 全局唯一、发布后不可变；「模板」只描述代码能力，「实例」才承载 API Key 等敏感配置（存 `plugin_dynamic.config`）。

| 模板 key | 实现 | 配置（TConfig） | 请求（TRequest） | 响应（TResponse） |
|---|---|---|---|---|
| `dynamic_greet` | `DynamicGreetPlugin` | `Prefix` | `Name` | `Message` |
| `bocha_web_search` | `BoChaWebSearchPlugin` | `ApiKey`（必填，`InitAsync` 校验） | `Query`(必填)/`Freshness`/`Summary`/`Count`/`Include`/`Exclude` | `Query`/`TotalEstimatedMatches`/`SomeResultsRemoved`/`WebPages[]`/`Images[]` |
| `bocha_ai_search` | `BoChaAiSearchPlugin` | `ApiKey`（必填，`InitAsync` 校验） | `Query`(必填)/`Freshness`/`Include`/`Count`/`Answer` | `Query`/`ConversationId`/`Answer`/`FollowUps[]`/`WebPages[]`/`Images[]`/`ModelCards[]`/`SomeResultsRemoved` |
| `feishu_webhook_text` | `FeishuWebhookTextPlugin` | `WebhookKey`（必填，`InitAsync` 校验）/`SignKey` | `Text`（必填，`RunAsync` 校验） | `Code`/`Msg`/`Text` |
| `javascript_executor` | `JavaScriptExecutorPlugin` | `JavaScriptCode`（必填，`InitAsync` 校验；必须导出 `run(parameter)`） | `Parameters`（必填字符串，`RunAsync` 透传给 `run`） | `Parameters`/`ResultJson`/`ResultKind`（值类型见 `JsResultKind`） |
| `postgres_query` | `PostgresQueryPlugin` | `ConnectionString`（必填，`InitAsync` 校验）/`MaxRows`(1-1000，默认 100)/`CommandTimeoutSeconds`(1-300，默认 30) | `Sql`（必填，`RunAsync` 校验为单条只读语句） | `Columns[]`/`Rows[]`/`RowCount`/`Truncated` |
| `mysql_query` | `MysqlQueryPlugin` | 同 `postgres_query`（连接串为 MySQL 语法） | 同上 | 同上 |
| `paddleocr_ocr` | `PaddleOcrPlugin` | `ApiUrl`（必填，`InitAsync` 校验；用户自有 PaddleOCR 服务地址）/`Token`（留空表示部署未开启鉴权） | `File`(必填，URL 或 Base64)/`FileType`/`UseDocOrientationClassify`/`UseDocUnwarping`/`UseTextlineOrientation` | `Pages[]`：每页含 `Text`（`rec_texts` 按行拼接）、`OcrImage`（Base64）、`InputImage`（Base64） |
| `paddleocr_structure_v3` | `PaddleStructureV3Plugin` | 同 `paddleocr_ocr` | `File`(必填)/`FileType`/`UseDocOrientationClassify`/`UseDocUnwarping`/`UseTableRecognition`/`UseFormulaRecognition`/`UseSealRecognition`/`UseChartRecognition`/`UseRegionDetection` | `Pages[]`：每页含 `PrunedResultJson`（原文 JSON）、`OutputImages`（按名索引的 Base64 字典）、`InputImage`、`SealTexts[]`（从 `seal_res_list.rec_texts` 逐条抽取） |
| `paddleocr_vl` | `PaddleVlPlugin` | 同 `paddleocr_ocr` | `File`(必填)/`FileType`/`UseDocOrientationClassify`/`UseDocUnwarping`/`UseLayoutDetection`/`UseChartRecognition`/`PrettifyMarkdown`/`ShowFormulaNumber` | `Pages[]`：每页含 `MarkdownText`、`MarkdownImages`（相对路径 → Base64）、`InputImage`、`PrunedResultJson` |
| `moji_weather` | `MojiWeatherPlugin` | `AppCode`（必填，`InitAsync` 校验）/`Token`（部分服务规格要求） | `CityId` 或 `Lat`+`Lon`（二选一，`RunAsync` 校验） | `City`/`Condition`/`Forecast[]` |

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
4. **连接串只来自实例配置**（面向用户自有的业务库，不是宿主库），不打印、不落日志；配置示例与 `[Description]` 均提示「建议为插件单独创建只读账号」。
5. **无状态**：每次运行由 `PluginExecutor` 创建独立 DI 作用域与实例，仅缓存 `InitAsync` clamp 后的配置；连接用后即释放（驱动连接池按连接串隔离），会话只读设置在每次运行时重新下发。
6. **两个模板各自持有模型**：与 `bocha_*` 系列一致，分别使用 `PostgresQuery*` / `MysqlQuery*`（便于后续按数据库差异扩展）；共用的是 `SqlReadOnlyGuard`、`SqlResultReader`、`SqlQueryResult` 三个内部辅助类型（`MoAI.AIPlugin.Dynamic/` 根目录，与 `BoChaAuthorization` 同级）。
7. **驱动与版本**：PostgreSQL 用 `Npgsql` 10.0.3、MySQL 用 `MySqlConnector` 2.5.0，版本均由 `Directory.Packages.props` 集中管理，插件项目只加 `PackageReference`（新增包引用后需重新 `dotnet restore`）。

## 已知问题

- `templete_key` 列名拼写沿用用户给定的 `templete_key`（非 `template_key`）；如需修正属数据/DB 迁移事项。
- 动态模板的配置仍以 `configJson` 在 `PluginExecutor.InitAsync` 校验；此处实例 `config` 直接作为该值传入。
- **管理员侧「实例 key 与其它实例重复 → 409」为死代码**：`SaveDynamicPluginCommandHandler` 先按 `plugin_key` 查已有实例，命中即走更新；未命中时 `EnsureInstanceKeyUniqueAsync` 的 DB 判重谓词（`plugin_key == key && IsDeleted == 0`）与前者完全相同，恒为 false。跨团队冲突由团队插件链路负责（teamplugin TP-13），如需在管理员侧禁止重名需另行定义语义（例如要求 key 携带团队/命名空间）。
- 插件运行结果的 `dataJson` 使用 PascalCase 字段名（`PluginExecutor` 以默认 `JsonSerializerOptions` 序列化响应对象），与 HTTP 接口的 camelCase 风格不一致；消费插件结果时需按 PascalCase 读字段。属引擎既有行为，改动会影响所有既有模板，未在本轮调整。
- 博查两模板的**成功路径**已由 `local-dev/bocha-search-e2e.mjs` 用桩服务自动覆盖（@DYN-S16 / @DYN-S22）；桩服务返回的是官方文档样例报文，**真实上游字段若与文档有出入仍需一次人工走查**。
- `bocha_ai_search` 的模态卡只解析通用字段：`douyin` 等非通用结构（`cover_images`、`interactions` 等）会被裁剪，仅保留 `Type` 与可映射的 `description`。
- `moji_weather` 按阿里云云市场公开样例报文（`{code,msg,data}` 信封 + camelCase 字段）实现并容错解析；墨迹官方完整接口文档需注册后获取，真实上游字段若与样例有出入仍需一次人工走查（同博查既有口径）。
