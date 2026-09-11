# 动态插件（DynamicPlugin）设计规格（SDD）

> 关联：[SDD](./sdd.md) ｜ [BDD](./bdd.md) ｜ [TDD](./tdd.md) ｜ [SOP](./sop.md) ｜ 上游：[../aiplugin-static/sdd.md](../aiplugin-static/sdd.md) ｜ 规范：[../cqrs-conventions.md](../cqrs-conventions.md) ｜ 证据：[local-dev/dynamic-plugin-e2e.mjs](../../local-dev/dynamic-plugin-e2e.mjs) ｜ [local-dev/bocha-search-e2e.mjs](../../local-dev/bocha-search-e2e.mjs)
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

`feishu_webhook_text` 细节：

1. **复用基础设施层客户端**：`MoAI.AIPlugin.Dynamic` 引用 `MoAI.Infra.ExternalHttp`，构造注入 `IFeishuWebHookClient`（Refit + `ExternalHttpMessageHandler`，路由模板 `bot/v2/hook/{key}`，BaseAddress `https://open.feishu.cn`）；不在插件内新建 `HttpClient`。**接口定义里 `key` 必须是 path 参数**（飞书 webhook 协议要求 token 出现在路径里），不要写成 `[Query] string key`——后者会让 Refit 把它附加成 `?key=xxx` 查询串，飞书会按 URL 错误处理。同时移除接口上的 `HttpClient Client { get; }` 暴露，避免诱导调用方自拼 URL。
2. **Webhook 输入归一**：配置可粘完整地址（含 `https://open.feishu.cn/open-apis/bot/v2/hook/{token}` 或仅 `{token}`），由 `NormalizeWebhookKey` 统一截取最后一个 `/hook/` 之后的 token，并切掉 `?`、`#`、尾随 `/`，避免用户粘贴查询串/路径斜杠导致飞书返回 `19001`。
3. **签名校验**：开启时填 `SignKey`，调用前给 `FeishuWebHookTextRequest` 补 `timestamp + sign`；未开启时保持为空，序列化时会被忽略。
4. **错误归一**：Refit 非 2xx → `BusinessException((int)StatusCode, "飞书推送失败（HTTP xxx）：{body}")`；HTTP 200 但响应 `code != 0` 按飞书文档码表映射——`19001` 400 地址无效、`19007` 400 机器人已禁用、`19021` 400 签名校验失败、`19022` 400 IP 不在白名单、`19024` 400 未含关键词、`9499` 429 触发限频、其余 `500`。`Success=false` 在 `PluginExecutor` 一并归一展示。
5. **参数兜底**：`Text` 为空抛 `BusinessException(400, "文本内容 Text 不能为空")`；`WebhookKey` 为空在 `InitAsync` 直接返可读错误，实例进入运行抽屉前即被拒。

## 已知问题

- `templete_key` 列名拼写沿用用户给定的 `templete_key`（非 `template_key`）；如需修正属数据/DB 迁移事项。
- 动态模板的配置仍以 `configJson` 在 `PluginExecutor.InitAsync` 校验；此处实例 `config` 直接作为该值传入。
- **管理员侧「实例 key 与其它实例重复 → 409」为死代码**：`SaveDynamicPluginCommandHandler` 先按 `plugin_key` 查已有实例，命中即走更新；未命中时 `EnsureInstanceKeyUniqueAsync` 的 DB 判重谓词（`plugin_key == key && IsDeleted == 0`）与前者完全相同，恒为 false。跨团队冲突由团队插件链路负责（teamplugin TP-13），如需在管理员侧禁止重名需另行定义语义（例如要求 key 携带团队/命名空间）。
- 插件运行结果的 `dataJson` 使用 PascalCase 字段名（`PluginExecutor` 以默认 `JsonSerializerOptions` 序列化响应对象），与 HTTP 接口的 camelCase 风格不一致；消费插件结果时需按 PascalCase 读字段。属引擎既有行为，改动会影响所有既有模板，未在本轮调整。
- 博查两模板的**成功路径**已由 `local-dev/bocha-search-e2e.mjs` 用桩服务自动覆盖（@DYN-S16 / @DYN-S22）；桩服务返回的是官方文档样例报文，**真实上游字段若与文档有出入仍需一次人工走查**。
- `bocha_ai_search` 的模态卡只解析通用字段：`douyin` 等非通用结构（`cover_images`、`interactions` 等）会被裁剪，仅保留 `Type` 与可映射的 `description`。
