# 静态插件（StaticPlugin）设计规格（SDD）

> 关联：[SDD](./sdd.md) ｜ [BDD](./bdd.md) ｜ [TDD](./tdd.md) ｜ [SOP](./sop.md) ｜ 上游：[../aiplugin-sdd](../database-scaffold/sdd.md) ｜ 规范：[../cqrs-conventions.md](../cqrs-conventions.md) ｜ 证据：[local-dev/static-plugin-e2e.mjs](../../local-dev/static-plugin-e2e.mjs)
> 规范：[../DOC-STANDARD.md](../DOC-STANDARD.md)。行为场景见 BDD（@STP-Sxx），本文不重复。

## 目标

在已有 `aiplugin` 插件引擎（`PluginRegistry` + `PluginExecutor`）基础上，落地**静态插件**的完整管理闭环：静态插件无实例、无配置，注册表内存中存在但 DB 默认无记录。需要实现：

1. **静态插件列表**：合并「内存注册表发现的静态插件」与「DB 中的静态插件记录」，去重后返回；无 DB 记录的插件分类归「未分类」。
2. **运行静态插件**：右侧抽屉，Monaco JSON 编辑器展示请求参数示例，运行后展示结果。
3. **编辑写回**：一旦修改静态插件信息（标题/描述/分类），写回或创建 DB 记录，下次查询归入对应分类。

## 组件

```
src/aiplugin/
├── MoAI.AIPlugin.Shared/
│   ├── Commands/SaveStaticPluginCommand.cs        {pluginKey,title,description,classifyId} → EmptyCommandResponse
│   ├── Queries/Responses/QueryPluginManageListCommandResponseItem.cs  （增强：+PluginKey,+ParamsExample）
│   └── Queries/QueryPluginManageListCommand.cs    （沿用，无需改）
├── MoAI.AIPlugin.Core/
│   ├── Commands/SaveStaticPluginCommandHandler.cs  校验 + 写回/创建 DB
│   └── Queries/QueryPluginManageListCommandHandler.cs  （增强：合并内存 + DB 去重）
├── MoAI.AIPlugin.Static/                          内置静态插件（Models/ + Plugins/ + Helpers/）
│   ├── Plugins/StaticEchoPlugin.cs                static_echo（示例）
│   ├── Plugins/JavaScriptExecutorPlugin.cs        static_javascript_executor（Jint 沙箱）
│   ├── Plugins/CurrentTimePlugin.cs               static_current_time（无参数）
│   ├── Plugins/FlowWaitPlugin.cs                  static_flow_wait（等待秒数）
│   ├── Plugins/MarkdownToHtmlPlugin.cs            static_markdown_to_html（Markdig）
│   ├── Plugins/TextExtractPlugin.cs               static_text_extract（下载 + TextExtractionService）
│   ├── Plugins/FileToMarkdownPlugin.cs            static_file_to_markdown（Url 下载 + 自动识别文件名 + TextExtractionService）
│   ├── Plugins/WebContentFetchPlugin.cs           static_web_content_fetch（下载 + AngleSharp）
│   └── Helpers/AngleSharpHelper.cs                网页正文提取
└── MoAI.AIPlugin.Api/
    └── Controllers/StaticPluginController.cs      [Route("/ai/plugin/static")]，门禁在 Controller
ui/src/
├── api/plugin.ts                                   +saveStaticPlugin, +runPlugin
└── pages/plugins/
    ├── Plugins.tsx                                静态 Tab：操作列（运行/编辑）+ 抽屉 + 编辑弹窗
    └── components/PluginRunDrawer.tsx              Monaco JSON 编辑器 + 运行结果
```

三层依赖：`Api → Core → Shared`。Api 引用 `MoAI.Account.Shared`（管理员校验）、`MoAI.AIPlugin.Shared`。

## API 契约

路由前缀 `/ai/plugin/static`，认证自动追加 `[Authorize]`；管理员门禁在 Controller 层（`GetUserStateAsync().IsAdmin`，否则 403「只有管理员可以管理插件」）。

| 方法 | 路由 | 门禁 | 说明 |
|---|---|---|---|
| POST | `/ai/plugin/static/save` | admin | `{pluginKey,title,description,classifyId}` → `EmptyCommandResponse`，写回/创建 DB |
| POST | `/ai/plugin/run`（沿用 `PluginController`） | admin | `{key,requestJson}` → `PluginRunResult`，运行静态插件 |

`QueryPluginManageListCommandResponseItem` 新增两个字段：
- `pluginKey`（string?）：仅静态插件有；内存发现但无 DB 记录时即为 key，用于编辑写回。
- `paramsExample`（string?）：仅静态插件有；来自 `PluginTypeHelper.GetStaticExample(...,"GetParamsExampleValue")`，抽屉 Monaco 初始值。

## 关键决策

1. **合并去重主键 = 插件 key**：内存发现（`registry.GetAll().Where(!IsDynamic)`）与 DB 记录（`PluginStatics ⋈ Plugins`）按 `pluginKey` 合并；**DB 记录优先**（已有分类/标题/描述），内存-only 项 `classifyId=0`、`classifyName=null`（前端渲染「未分类」）、`IsSystem=true`。
2. **key 不可变**：key 是引擎注册标识，编辑不修改 key；编辑只改 title/description/classifyId。
3. **写回策略**：DB 存在 `plugin_key == pluginKey` 的记录则更新；否则**新增** `PluginEntity`（`IsSystem=true`、`TeamId=0`、`Type=native`、`PluginName=pluginKey`、`ClassifyId=请求值`）+ `PluginStaticEntity`（`PluginKey=pluginKey`）。新增只在用户首次编辑时发生。
4. **分类校验**：`classifyId` 若非 0 需在 `classify` 表存在且 `Type=plugin`，否则 400；允许 0（未分类）。
5. **运行接口复用**：静态插件已由 `RunPluginCommand` / `PluginExecutor` 处理（静态无 configJson），前端直接复用 `POST /ai/plugin/run`，不新增运行端点。
6. **前端编辑器**：`@monaco-editor/react`（Monaco）——与 VS Code 同款内核，支持 JSON 高亮/校验/折叠，符合「vscode 编辑器」诉求；抽屉 `maskClosable={false}`。
7. **门禁位置**：权限只在 Controller 层（admin），Handler 层不注入用户上下文；目标数据规则（key 定位、分类校验）在 Handler 层。
8. **i18n**：新增文案同时写 `zh-CN` 与 `en-US` `common.json`。

## 内置插件（迁移）

旧 `MoAI.Plugin.Tool` 的 5 个工具插件按最新静态机制迁移到 `MoAI.AIPlugin.Static`：`static_current_time`、`static_flow_wait`、`static_markdown_to_html`、`static_text_extract`、`static_web_content_fetch`；原 `javascript_executor_nohave_paramter` 已先行迁移为 `static_javascript_executor`。统一采用 `static_` 前缀 key、`IStaticPluginRuntime<TRequest,TResponse>` 强类型请求/响应模型。

- 依赖：`Markdig`（Markdown→HTML）、`AngleSharp`（网页正文解析）、`Maomi.ToMarkdown`（`TextExtractionService` 按文件名后缀选抽取器，由 `WikiCoreModule.AddTextExtraction()` 注册）。
- 外部下载统一走 infra 客户端 `IPutClient`（构造注入，复用 `ExternalHttpMessageHandler` 日志/遥测），不在插件内 `new HttpClient`。
- 文本提取只接受 http/https 文件地址（旧实现 `new Uri(url)` 亦只支持绝对地址），不支持本地路径。
- 迁移清单之外新增 `static_file_to_markdown`：与 `static_text_extract` 同源（`TextExtractionService` + `IPutClient`），差异是只需 `Url`（`FileName` 留空时从 Url 路径末段自动识别并 URL 解码，传了 FileName 则要求带扩展名），并在**下载前**按扩展名预检 MIME（`MimeTypesDetection.TryGetFileType`），响应返回 `markdown` + `fileName` + `fileType`。
- 行为场景见 [BDD @STP-S10~S17](./bdd.md#feature-内置静态插件)；验证见 [TDD](./tdd.md)。

## 已知问题

- 动态插件（dynamic）的本轮不做，仅静态；动态实例管理（多实例、key 唯一、配置编辑）后续迭代。
- `paramsExample`/`pluginKey` 只对静态插件有值；自定义（custom）插件走现有 `CustomPluginPanel`，不涉及本模块。
- Monaco 体积较大（worker 按需加载），首次打开抽屉可能短暂白屏（可接受，后续可做懒加载）。
