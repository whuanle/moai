# 插件编写规范（AI Plugin Authoring）

> 本文是**插件开发者必读**的契约文档，规定 MoAI 静态内置插件与 MoPlatform 动态插件包的编写规则。MoAI 不再将动态插件模板编译进宿主；新动态插件在 `F:\workspace\mo_plugins` 制作并经 MoPlatform 分发。
> 配套：设计规格见 [静态插件 SDD](./aiplugin-static/sdd.md)、[动态插件 SDD](./aiplugin-dynamic/sdd.md)；通用分层规范见 [cqrs-conventions.md](./cqrs-conventions.md)。

## 一、总体概念

插件分两类，核心区别是**是否独立于请求之外的配置**：

| 类型 | 配置 | 实例 | 运行方式 | 列表行为 |
|---|---|---|---|---|
| 静态插件 | 无 | 无（一个模板即一个插件） | 直接传请求参数运行 | 内存注册表 + DB 记录合并去重；无 DB 记录归「未分类」 |
| 动态插件 | 有（`TConfig`） | 有（一个模板可建多个实例，实例 key 全局唯一） | 先创建实例填配置，再以实例 key 运行 | 只展示已创建实例；空白模板不出现 |

两者都运行于 `IPluginExecutor`，后端通过 `IPluginRegistry` 扫描发现。静态插件随宿主内置；动态插件通过管理端从 MoPlatform 安装后进入 `configs/plugins`，动态实例仍保存各自配置。

## 二、插件发现机制（重要）

插件由 `PluginRegistry` 在**首次访问**时扫描当前 `AppDomain` 中**引用了契约程序集 `MoAI.AIPlugin.Shared`** 的程序集，对每个类型调用 `PluginTypeHelper.TryGetPluginInfo` 发现。

**因此编写插件必须满足：**
1. 插件类必须标记 `[AiPlugin(key: "...")]`。
2. 插件类必须实现 `IStaticPluginRuntime<TRequest,TResponse>` 或 `IDynamicPluginRuntime<TRequest,TResponse,TConfig>`。
3. 插件所在程序集必须 `ProjectReference` 到 `MoAI.AIPlugin.Shared`（否则不满足「引用契约程序集」条件，不会被扫描）。
4. 插件类必须是**公开**的、非 abstract、非 interface、非泛型定义。
5. 静态插件程序集需被宿主 `src/MoAI` 以 `ProjectReference`/模块挂载；动态插件作为 MoPlatform 包安装到 `configs/plugins/{key}`，由启动加载器通过独立 `AssemblyLoadContext` 发现，不得把它加回宿主项目引用。

> 注意：只要插件类实现上述运行时接口且带 `[AiPlugin]`，就会被识别；与是否注册进 DI 无关（执行时由 `ActivatorUtilities.CreateInstance` 按需实例化）。

## 三、`[AiPlugin]` 特性

声明在插件类上，`key` 必填。`HumanDescription` 面向管理员和用户展示插件用途；`Description` 面向 AI，写清能力、参数、约束和调用方式：

```csharp
[AiPlugin(key: "static_echo", Name = "静态回显", HumanDescription = "用于测试插件调用和验证参数传递。", Description = "回显请求参数，无需额外配置")]
public class StaticEchoPlugin : IStaticPluginRuntime<StaticEchoRequest, StaticEchoResponse>
```

- `key`：插件模板唯一标识（注册表主键），**不区分大小写**存储、`OrdinalIgnoreCase` 比较。
- `key` 建议 `蛇形命名`（全小写 + 下划线），避免与动态插件实例 key 冲突。
- `key` 一旦发布**不可变**：它被 `plugin_static.plugin_key` / `plugin_dynamic.templete_key` 以及代码内引用，改 key 会导致历史记录失效。
- `HumanDescription` 用于插件目录和管理列表，建议写 1–2 句、约 50–100 个中文字符，说明核心能力、典型场景和能解决的问题；避免参数细节。`Description` 用于 AI 工具选择和调用，写清调用所需能力、参数与限制。

## 四、请求 / 响应 / 配置模型

- `TRequest`：请求参数模型，运行抽屉的 Monaco 初始值来自 `GetParamsExampleValue()`。
- `TResponse`：响应结果模型，运行结果 `dataJson` 按该类型序列化。
- `TConfig`（仅动态）：配置模型，创建实例时由 Monaco 编辑、经 `InitAsync` 校验。
- 模型成员建议加 `[Description("...")]` 注解，便于工具生成文档。

**示例：**
```csharp
public class StaticEchoRequest
{
    [Description("待回显的消息")]
    public string Message { get; set; } = string.Empty;
}
```

## 五、运行时接口

### 静态插件 `IStaticPluginRuntime<TRequest,TResponse>`

```csharp
public class StaticEchoPlugin : IStaticPluginRuntime<StaticEchoRequest, StaticEchoResponse>
{
    public static string GetParamsExampleValue()
        => """{"Message":"hello"}""";

    public Task<StaticEchoResponse> RunAsync(StaticEchoRequest request, CancellationToken ct)
        => Task.FromResult(new StaticEchoResponse { Message = $"echo:{request.Message}" });
}
```

### 动态插件 `IDynamicPluginRuntime<TRequest,TResponse,TConfig>`

额外实现 `GetConfigExampleValue()` 与 `InitAsync(TConfig)`：

```csharp
public class DynamicGreetPlugin : IDynamicPluginRuntime<DynamicGreetRequest, DynamicGreetResponse, DynamicGreetConfig>
{
    private DynamicGreetConfig? _config;

    public static string GetParamsExampleValue() => """{"Name":"MoAI"}""";
    public static string GetConfigExampleValue() => """{"Prefix":"Hello"}""";

    public Task<string?> InitAsync(DynamicGreetConfig config)
    {
        if (string.IsNullOrWhiteSpace(config.Prefix))
            return Task.FromResult<string?>("Prefix 不能为空");
        _config = config;
        return Task.FromResult<string?>(null);
    }

    public Task<DynamicGreetResponse> RunAsync(DynamicGreetRequest request, CancellationToken cancellationToken)
        => Task.FromResult(new DynamicGreetResponse { Message = $"{_config?.Prefix} {request.Name}" });
}
```

### 上下文注入（获取当前用户，可选）

插件需要**调用方身份**（谁在运行这个插件）时，构造注入 `IPluginRunContextAccessor`（静态/动态通用）。执行引擎在实例化插件前把调用方上下文写入插件作用域，插件无需读 HTTP 上下文（工作流/外部会话等非 HTTP 环境没有它）：

```csharp
public class CurrentUserPlugin : IStaticPluginRuntime<CurrentUserRequest, CurrentUserResponse>
{
    private readonly IPluginRunContextAccessor _runContextAccessor;

    public CurrentUserPlugin(IPluginRunContextAccessor runContextAccessor)
    {
        _runContextAccessor = runContextAccessor;
    }

    public async Task<CurrentUserResponse> RunAsync(CurrentUserRequest request, CancellationToken cancellationToken)
    {
        var context = _runContextAccessor.Context;          // UserId/UserType/TeamId/Source，历史路径未注入时为 null
        var user = context?.IsAuthenticated == true
            ? await _runContextAccessor.GetUserAsync(cancellationToken)   // 懒加载用户名/昵称/邮箱（查 user 表，单次执行内缓存）
            : null;
        // ...映射响应
    }
}
```

上下文由三个执行入口自动构建：插件管理页运行（`Source=admin`）、团队插件运行（`Source=team`，带 TeamId）、应用 AI 工具调用（`Source=agent`，来自 `AppAgentBuildContext`）。新增执行入口时须显式传 `PluginRunContext` 给 `IPluginExecutor.ExecuteAsync`，否则插件读到 null。设计细节见 [静态插件 SDD 执行上下文注入](./aiplugin-static/sdd.md#执行上下文注入stp-s18s19)。

### 文件存储（可选）

插件需要返回图片、导出 PDF/Word 等二进制产物时，构造注入 `IPluginFileStorageService`（静态/动态通用）。该服务定义在 `MoAI.AIPlugin.Shared`，插件**只需依赖插件共享层**，不需要引用 `MoAI.Storage.Shared` 或 S3 存储模块：

```csharp
public class ChartPlugin : IStaticPluginRuntime<ChartRequest, ChartResponse>
{
    private readonly IPluginFileStorageService _files;

    public ChartPlugin(IPluginFileStorageService files)
    {
        _files = files;
    }

    public async Task<ChartResponse> RunAsync(ChartRequest request, CancellationToken cancellationToken)
    {
        // 1) 保存流/字节，返回文件 id（按 SHA-256 内容寻址，同内容幂等去重）
        var fileId = await _files.SaveAsync(stream, new PluginFileSaveOptions
        {
            FileName = "chart.png",
            ContentType = "image/png",
            IsPublic = true,          // 图片等可直接展示的产物放公开目录
            Prefix = "images",        // 实际 ObjectKey：public/images/{sha256}.png
        }, cancellationToken);

        // 2) 公开文件用不过期的 /static 地址，适合在 Markdown 中渲染图片
        var publicUrl = await _files.GetPublicUrlAsync(fileId, cancellationToken);

        // 3) 敏感导出文件保持私有，按 id 取预签名下载地址（有有效期，需在用户点击时现取）
        // var downloadUrl = await _files.GetDownloadUrlAsync(fileId, "report.pdf", cancellationToken);
        return new ChartResponse { Url = publicUrl!.ToString() };
    }
}
```

- **公网文件转存**：`SaveFromUrlAsync(url, options, cancellationToken)` 会下载 http/https 文件并落存储，`FileName` 留空时从响应头或地址自动识别。
- **公开与私有**：`IsPublic=true` 的文件通过 `/static/{objectKey}` 免登录访问、地址不会过期，只适合图片/音视频等可公开产物；包含敏感内容的 PDF/Word 应保持私有，用文件 id 获取预签名下载地址（有效期由存储层统一控制，不要长期缓存该地址）。
- **查询文件信息**：`GetFileInfoAsync(fileId)` 返回内容类型、大小、扩展名、SHA-256 与公开地址（私有文件 `PublicUrl=null`）。
- 禁止插件直接操作 `file` 表、拼接 OSS 地址或自行 `new HttpClient` 下载文件；需要更多存储能力时扩展该服务，而不是把存储模块引用带回插件。

## 六、动态插件 `InitAsync` 校验规则

- `InitAsync` 返回 `null` 表示校验通过，返回**非空字符串**表示校验失败（该字符串即为错误提示）。
- 在 `InitAsync` 中把校验通过的配置保存到实例字段，供后续 `RunAsync` 使用。
- 校验失败不抛异常，直接返回错误字符串；执行引擎会把它封装成 `PluginRunResult{Success=false}`。
- 每次运行都会重新实例化插件并调用 `InitAsync`（`PluginExecutor` 为每次执行创建独立 DI 作用域），**不要在插件构造器里做依赖外部配置的初始化**。

## 七、JSON 与序列化约定

- 请求/配置反序列化使用 `PluginExecutor._deserializeOptions`，**允许 JSON 注释 `//` 与尾随逗号**（`ReadCommentHandling=Skip` + `AllowTrailingCommas`），`PropertyNameCaseInsensitive=true`。
- 因此 `GetParamsExampleValue()` / `GetConfigExampleValue()` 的示例 JSON 里可写注释、可带尾随逗号，用户在前端 Monaco 编辑时也会被接受。
- 响应结果由引擎序列化为 `dataJson`，前端 Monaco 会格式化展示。
- `GetParamsExampleValue()` / `GetConfigExampleValue()` 必须是 `static abstract`（接口的静态抽象成员），实现类用 `public static`。

## 八、目录与模块放置

遵循 CQRS 分层，插件实现类放在业务子模块：

```
src/aiplugin/
├── MoAI.AIPlugin.Static/    Models/（请求/响应模型）+ Plugins/（MoAI 内置静态插件）+ StaticPluginsModule
├── MoAI.AIPlugin.Dynamic/   历史内置模板实现（不再由 MoAI 宿主挂载；保留为迁移前资料/测试）
├── MoAI.AIPlugin.Shared/    Attributes/、Contracts/、Models/、Services/（插件契约与共享服务）、Commands/、Queries/（契约与命令定义）
├── MoAI.AIPlugin.Core/      Services/（Registry/Executor）、Commands/、Queries/（Handler、业务逻辑）
├── MoAI.AIPlugin.Custom/    Commands/、Queries/、Services/（DB 相关的 Handler、实例解析器）
└── MoAI.AIPlugin.Api/       Controllers/（admin 门禁）
```

约定：
- MoAI 自带的静态插件放 `MoAI.AIPlugin.Static`；在线动态插件放 `F:\workspace\mo_plugins` 并按 [MoPlatform 插件包协议](../aiplugin-dynamic/sdd.md#mo-platform-在线插件分发) 发布。
- 新增**静态内置插件**必须在宿主 `src/MoAI/MainModule.cs` 通过 `[InjectModule<...>]` 挂载所在模块；动态包由 `configs/plugins` 加载器发现，不要挂载 `DynamicPluginsModule`。
- Handler 涉及 DB 时放 `MoAI.AIPlugin.Custom`（它引用 `MoAI.Database.Shared` 与 `MoAI.Classify.Shared`）；无 DB 的逻辑放 `MoAI.AIPlugin.Core`。
- **插件需要调用外部 HTTP 接口时**：Refit 客户端实现放 `src/infra/MoAI.Infra.ExternalHttp/<厂商>/`（复用 `ExternalHttpMessageHandler` 的日志与遥测，典型：`BoCha`），插件项目加 `ProjectReference` 后**构造注入**该客户端（`PluginExecutor` 用 `ActivatorUtilities.CreateInstance` 从 DI 作用域解析），不要在插件内 `new HttpClient`。
- **插件需要解析上游 JSON 字符串时**（很多厂商把结构化结果塞进 `content` 字符串）：不要用强类型直接反序列化，用 `JsonDocument` 逐层展开并写**容错**分支（字段缺失、形态变化、空对象都要能跳过而不是抛异常），并把「取不到任何有效字段」的条目丢弃。
- **插件需要把二进制产物落文件存储时**（解码图片、生成文件等）：构造注入 `IPluginFileStorageService`（只依赖 `MoAI.AIPlugin.Shared`），用 `SaveAsync(stream/bytes, PluginFileSaveOptions)` 落存储并拿文件 id；图片等公开产物设 `IsPublic=true` 后用 `GetPublicUrlAsync` 拼公开地址，敏感导出文件用 `GetDownloadUrlAsync` 取预签名地址，禁止直接操作 file 表或 OSS。详见[文件存储（可选）](#文件存储可选)。参考实现：动态插件 `Plugins/Base64DecodePlugin.cs`。
- 参考实现：动态插件 `Plugins/BoChaWebSearchPlugin.cs`（外部 HTTP + 实例配置校验）、`Plugins/BoChaAiSearchPlugin.cs`（外部 HTTP + `content` JSON 文本解析 + 多形态结果重组），静态插件 `Plugins/StaticEchoPlugin.cs`（无配置）、`Plugins/CurrentUserPlugin.cs`（执行上下文注入取当前用户）。
- 内置静态插件清单见 [静态插件 SOP 种子说明](./aiplugin-static/sop.md#种子说明)。新增静态插件可按形态挑选模板：`Plugins/CurrentTimePlugin.cs`（无参数）、`Plugins/FlowWaitPlugin.cs`（数值参数 + 业务校验 + `Task.Delay` 取消）、`Plugins/MarkdownToHtmlPlugin.cs`（第三方库转换）、`Plugins/TextExtractPlugin.cs`（构造注入 `Maomi.ToMarkdown.TextExtractionService` + `IPutClient` 下载）、`Plugins/WebContentFetchPlugin.cs`（外部 HTTP + AngleSharp 解析 + 超时/取消归一）。静态插件调用第三方库时需在 `MoAI.AIPlugin.Static.csproj` 显式加包引用（如 `Markdig`、`AngleSharp`、`Maomi.ToMarkdown`）。
- **验证插件成功路径不要依赖真实 Key**：上游地址若能配置覆盖，用本地桩服务 + 真实后端跑端到端（见 `local-dev/bocha-search-e2e.mjs`）；桩服务只回放厂商文档的样例报文即可覆盖「请求下发 → 鉴权 → 解析 → 结果落库/返回」整条链路。

## 九、运行时流程（后端）

1. MoAI 启动时扫描 DB 安装记录与 `configs/plugins/{key}`，使用每包独立 ALC 加载启用包；静态内置插件仍经模块挂载加载。
2. 前端调用 `POST /ai/plugin/run`，body `{ key, requestJson }`（动态实例**无需**前端传 config）。
3. `RunPluginCommandHandler`：
   - 先 `_registry.Get(key)`；
   - 未命中 → `IDynamicInstanceResolver.Resolve(key)`（查 `plugin_dynamic` 按实例 key → `templete_key` → 注册表取模板），返回该实例的 `configJson`；
   - 仍未命中 → 404「插件不存在」。
4. `PluginExecutor.ExecuteAsync(template, requestJson, configJson, context)`：
   - 隔离作用域内先注入执行上下文（调用方身份），再实例化插件；
   - 动态插件先 `InitAsync(configJson)`，失败返回错误；
    - `RunAsync(request)`，成功序列化 `dataJson`/`ResponseType`；异常归一化为 `Success=false, Error`。
5. 在线插件下架只从 registry/tool discovery 隐藏并阻止运行；其包目录及非可回收 ALC 都保留，不提供 unload/uninstall。

## 十、动态插件实例 key 约定

- 实例 key 存 `plugin_dynamic.plugin_key`；模板 key 存 `plugin_dynamic.templete_key`。
- 实例 key 规则：`^[a-z_][a-z0-9_]*$`（全小写+下划线，不能以数字开头），长度 ≤30，全局唯一。
- 唯一性校验：不与注册表任何模板 key 重复，也不与其它动态实例 key 重复（否则 409）。
- 实例 key 创建后不可变；编辑实例只能改 `templete_key/config/title/description/classify_id`。

## 十一、前端接入

- 插件实现后需重新生成 Kiota 客户端（`cd ui && npm run syncapi`，需后端 :5000 运行中暴露 OpenAPI）。
- 静态插件：`ui/src/pages/plugins/Plugins.tsx` 静态 Tab（复用 `PluginPanel` + `PluginRunDrawer`）。
- 在线插件：`ui/src/pages/plugins/PluginMarketplacePanel.tsx` 浏览 MoPlatform 目录、安装包、上架/下架；无卸载按钮。动态实例页继续用 `DynamicPluginPanel.tsx` 为已安装动态包模板配置实例，文案走 `ui/src/i18n/locales/{zh-CN,en-US}/common.json` 的 `plugins` 节点，zh/en 同步改。
- 测试：`ui/src/pages/plugins/__tests__/` 下补对应 `*.test.tsx`。

## 十二、验证命令（提交前全绿）

> 迁移说明（2026-11-14）：下列动态插件模板桩 E2E 是在线包改造前的历史证据，不作为当前宿主验收；在线插件当前验收以 MoAI 在线包单测和 `docs/aiplugin-dynamic/tdd.md` 的手工联调场景为准。

```bash
dotnet build src/MoAI/MoAI.csproj          # 后端 0 error
cd ui && npm run typecheck && npm run lint && npm run test   # 前端全绿
node local-dev/dynamic-plugin-e2e.mjs      # 迁移前动态模板 E2E 历史证据；不作为当前宿主验收（需后端 5000 运行中）
node local-dev/bocha-search-e2e.mjs        # 调用外部 HTTP 的插件：桩服务覆盖成功路径（无需真实 Key）
node local-dev/jina-search-e2e.mjs         # Jina 搜索/网页阅读：同一桩按请求体 q/url 区分端点（无需真实 Key）
node local-dev/hot-board-e2e.mjs           # 新闻热榜：16 域名数据桩经配置网关改写覆盖全渠道成功路径（无需真实外网）
```

## 常见问题

| 症状 | 原因 | 处理 |
|---|---|---|
| 插件不被发现 | 程序集未引用 `MoAI.AIPlugin.Shared`，或未挂载到 `MainModule` | 加 ProjectReference / InjectModule |
| 插件类被忽略 | 类为 abstract/非公开/泛型定义，或无 `[AiPlugin]` | 改为公开具体类 + 特性 |
| 运行报「必须提供配置」 | 动态插件执行时 configJson 为空 | 先创建实例，确认 `plugin_dynamic.config` 有值；或 `RunPluginCommand` 传 config |
| 运行报「请求参数解析失败」 | 请求 JSON 与模型不匹配 | 以 Monaco 参数示例为准，可含注释/尾随逗号 |
| 动态实例 key 冲突 | key 与模板或其它实例重复 | 更换实例 key |
