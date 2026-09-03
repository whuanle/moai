# 插件编写规范（AI Plugin Authoring）

> 本文是**插件开发者必读**的契约文档，规定静态插件与动态插件的编写规则，避免后续新增插件时规范混乱。
> 配套：设计规格见 [静态插件 SDD](./aiplugin-static/sdd.md)、[动态插件 SDD](./aiplugin-dynamic/sdd.md)；通用分层规范见 [cqrs-conventions.md](./cqrs-conventions.md)。

## 一、总体概念

插件分两类，核心区别是**是否独立于请求之外的配置**：

| 类型 | 配置 | 实例 | 运行方式 | 列表行为 |
|---|---|---|---|---|
| 静态插件 | 无 | 无（一个模板即一个插件） | 直接传请求参数运行 | 内存注册表 + DB 记录合并去重；无 DB 记录归「未分类」 |
| 动态插件 | 有（`TConfig`） | 有（一个模板可建多个实例，实例 key 全局唯一） | 先创建实例填配置，再以实例 key 运行 | 只展示已创建实例；空白模板不出现 |

两者都运行于 `IPluginExecutor`，进入「插件管理 → 对应 Tab」测试运行，后端通过 `IPluginRegistry` 扫描发现。

## 二、插件发现机制（重要）

插件由 `PluginRegistry` 在**首次访问**时扫描当前 `AppDomain` 中**引用了契约程序集 `MoAI.AIPlugin.Shared`** 的程序集，对每个类型调用 `PluginTypeHelper.TryGetPluginInfo` 发现。

**因此编写插件必须满足：**
1. 插件类必须标记 `[AiPlugin(key: "...")]`。
2. 插件类必须实现 `IStaticPluginRuntime<TRequest,TResponse>` 或 `IDynamicPluginRuntime<TRequest,TResponse,TConfig>`。
3. 插件所在程序集必须 `ProjectReference` 到 `MoAI.AIPlugin.Shared`（否则不满足「引用契约程序集」条件，不会被扫描）。
4. 插件类必须是**公开**的、非 abstract、非 interface、非泛型定义。
5. 插件程序集需被宿主 `src/MoAI` 参与（作为正常的 `ProjectReference` 模块被加载），才能进入 `AppDomain`。

> 注意：只要插件类实现上述运行时接口且带 `[AiPlugin]`，就会被识别；与是否注册进 DI 无关（执行时由 `ActivatorUtilities.CreateInstance` 按需实例化）。

## 三、`[AiPlugin]` 特性

声明在插件类上，`key` 必填，`Name`/`Description` 为展示信息：

```csharp
[AiPlugin(key: "static_echo", Name = "静态回显", Description = "回显请求参数，无需额外配置")]
public class StaticEchoPlugin : IStaticPluginRuntime<StaticEchoRequest, StaticEchoResponse>
```

- `key`：插件模板唯一标识（注册表主键），**不区分大小写**存储、`OrdinalIgnoreCase` 比较。
- `key` 建议 `蛇形命名`（全小写 + 下划线），避免与动态插件实例 key 冲突。
- `key` 一旦发布**不可变**：它被 `plugin_static.plugin_key` / `plugin_dynamic.templete_key` 以及代码内引用，改 key 会导致历史记录失效。

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

    public Task<DynamicGreetResponse> RunAsync(DynamicGreetRequest request, CancellationToken ct)
        => Task.FromResult(new DynamicGreetResponse { Message = $"{_config?.Prefix} {request.Name}" });
}
```

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
├── MoAI.AIPlugin.Static/    Models/（请求/响应模型）+ Plugins/（静态插件实现）+ StaticPluginsModule
├── MoAI.AIPlugin.Dynamic/   Models/（请求/响应/配置模型）+ Plugins/（动态插件实现）+ DynamicPluginsModule
├── MoAI.AIPlugin.Shared/    Attributes/、Contracts/、Models/、Commands/、Queries/（契约与命令定义）
├── MoAI.AIPlugin.Core/      Services/（Registry/Executor）、Commands/、Queries/（Handler、业务逻辑）
├── MoAI.AIPlugin.Custom/    Commands/、Queries/、Services/（DB 相关的 Handler、实例解析器）
└── MoAI.AIPlugin.Api/       Controllers/（admin 门禁）
```

约定：
- **示例/内置插件**放 `MoAI.AIPlugin.Static`、`MoAI.AIPlugin.Dynamic`，模型放各自 `Models/`。
- 新增插件**必须**在宿主 `src/MoAI/MainModule.cs` 通过 `[InjectModule<...>]` 挂载所在模块，否则程序集不参与扫描。
- Handler 涉及 DB 时放 `MoAI.AIPlugin.Custom`（它引用 `MoAI.Database.Shared` 与 `MoAI.Classify.Shared`）；无 DB 的逻辑放 `MoAI.AIPlugin.Core`。

## 九、运行时流程（后端）

1. 前端调用 `POST /ai/plugin/run`，body `{ key, requestJson }`（动态插件**无需**前端传 config）。
2. `RunPluginCommandHandler`：
   - 先 `_registry.Get(key)`；
   - 未命中 → `IDynamicInstanceResolver.Resolve(key)`（查 `plugin_dynamic` 按实例 key → `templete_key` → 注册表取模板），返回该实例的 `configJson`；
   - 仍未命中 → 404「插件不存在」。
3. `PluginExecutor.ExecuteAsync(template, requestJson, configJson)`：
   - 隔离作用域实例化插件；
   - 动态插件先 `InitAsync(configJson)`，失败返回错误；
   - `RunAsync(request)`，成功序列化 `dataJson`/`ResponseType`；异常归一化为 `Success=false, Error`。

## 十、动态插件实例 key 约定

- 实例 key 存 `plugin_dynamic.plugin_key`；模板 key 存 `plugin_dynamic.templete_key`。
- 实例 key 规则：`^[a-z_][a-z0-9_]*$`（全小写+下划线，不能以数字开头），长度 ≤30，全局唯一。
- 唯一性校验：不与注册表任何模板 key 重复，也不与其它动态实例 key 重复（否则 409）。
- 实例 key 创建后不可变；编辑实例只能改 `templete_key/config/title/description/classify_id`。

## 十一、前端接入

- 插件实现后需重新生成 Kiota 客户端（`cd ui && npm run syncapi`，需后端 :5210 运行中暴露 OpenAPI）。
- 静态插件：`ui/src/pages/plugins/Plugins.tsx` 静态 Tab（复用 `PluginPanel` + `PluginRunDrawer`）。
- 动态插件：`ui/src/pages/plugins/DynamicPluginPanel.tsx`（实例列表、新建/编辑弹窗 Monaco 配置、运行、删除），文案走 `ui/src/i18n/locales/{zh-CN,en-US}/common.json` 的 `plugins` 节点，zh/en 同步改。
- 测试：`ui/src/pages/plugins/__tests__/` 下补对应 `*.test.tsx`。

## 十二、验证命令（提交前全绿）

```bash
dotnet build src/MoAI/MoAI.csproj          # 后端 0 error
cd ui && npm run typecheck && npm run lint && npm run test   # 前端全绿
```

## 常见问题

| 症状 | 原因 | 处理 |
|---|---|---|
| 插件不被发现 | 程序集未引用 `MoAI.AIPlugin.Shared`，或未挂载到 `MainModule` | 加 ProjectReference / InjectModule |
| 插件类被忽略 | 类为 abstract/非公开/泛型定义，或无 `[AiPlugin]` | 改为公开具体类 + 特性 |
| 运行报「必须提供配置」 | 动态插件执行时 configJson 为空 | 先创建实例，确认 `plugin_dynamic.config` 有值；或 `RunPluginCommand` 传 config |
| 运行报「请求参数解析失败」 | 请求 JSON 与模型不匹配 | 以 Monaco 参数示例为准，可含注释/尾随逗号 |
| 动态实例 key 冲突 | key 与模板或其它实例重复 | 更换实例 key |
