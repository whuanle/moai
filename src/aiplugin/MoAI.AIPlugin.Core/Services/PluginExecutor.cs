using System;
using System.Linq;
using System.Reflection;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using MoAI.AIPlugin.Contracts;
using MoAI.AIPlugin.Models;
using MoAI.Infra.Exceptions;

namespace MoAI.AIPlugin.Services;

/// <summary>
/// 插件执行引擎的默认实现。为每次执行创建独立 DI 作用域，实例化插件并调用运行方法，实现调用间状态隔离.
/// </summary>
public class PluginExecutor : IPluginExecutor
{
    /// <summary>
    /// 反序列化请求/配置时的选项：允许 JSON 注释与尾随逗号（用户可能在 Monaco 编辑器里写注释或多余逗号）.
    /// </summary>
    private static readonly JsonSerializerOptions _deserializeOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        AllowTrailingCommas = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IPluginRegistry _registry;

    /// <summary>
    /// Initializes a new instance of the <see cref="PluginExecutor"/> class.
    /// </summary>
    /// <param name="scopeFactory">用于为每次执行创建隔离作用域.</param>
    /// <param name="registry">插件注册表，用于按 key 解析插件元数据.</param>
    public PluginExecutor(IServiceScopeFactory scopeFactory, IPluginRegistry registry)
    {
        _scopeFactory = scopeFactory;
        _registry = registry;
    }

    /// <inheritdoc/>
    public async Task<PluginRunResult> ExecuteAsync(PluginInfo plugin, string requestJson, string? configJson, PluginRunContext? context, CancellationToken cancellationToken, string? toolKey = null)
    {
        using var scope = _scopeFactory.CreateScope();

        // 注入执行上下文须先于插件实例化：插件在构造注入 IPluginRunContextAccessor 时即可读到
        scope.ServiceProvider.GetService<IPluginRunContextAccessor>()?.Set(context);

        object instance;
        try
        {
            instance = ActivatorUtilities.CreateInstance(scope.ServiceProvider, plugin.PluginType);
        }
#pragma warning disable CA1031 // 插件执行引擎需吞掉插件侧异常并归一化为结果，属预期行为
        catch (Exception ex)
        {
            return Fail(plugin, "插件实例化失败", ex);
        }
#pragma warning restore CA1031

        try
        {
            if (plugin.IsDynamic)
            {
                if (string.IsNullOrWhiteSpace(configJson))
                {
                    return Fail(plugin, "动态插件必须提供配置", null);
                }

                var initError = await InitDynamicAsync(instance, plugin, configJson, cancellationToken).ConfigureAwait(false);
                if (initError != null)
                {
                    return new PluginRunResult { Key = plugin.Key, Success = false, Error = initError };
                }
            }

            var result = await RunAsync(instance, plugin, requestJson, toolKey, cancellationToken).ConfigureAwait(false);
            var responseType = plugin.IsMultiTool ? plugin.FindTool(toolKey)!.Response : plugin.Response!;
            return new PluginRunResult
            {
                Key = plugin.Key,
                Success = true,
                DataJson = System.Text.Json.JsonSerializer.Serialize(result, responseType),
                ResponseType = responseType.FullName,
            };
        }
#pragma warning disable CA1031 // 插件执行引擎需吞掉插件侧异常并归一化为结果，属预期行为
        catch (Exception ex)
        {
            return Fail(plugin, "插件执行失败", ex);
        }
#pragma warning restore CA1031
        finally
        {
            await DisposeAsync(instance).ConfigureAwait(false);
        }
    }

    /// <inheritdoc/>
    public async Task<PluginRunResult> ExecuteAsync(string key, string requestJson, string? configJson, PluginRunContext? context, CancellationToken cancellationToken, string? toolKey = null)
    {
        var plugin = _registry.Get(key);
        if (plugin == null)
        {
            return new PluginRunResult { Key = key, Success = false, Error = "插件不存在" };
        }

        return await ExecuteAsync(plugin, requestJson, configJson, context, cancellationToken, toolKey).ConfigureAwait(false);
    }

    private static async Task<string?> InitDynamicAsync(object instance, PluginInfo plugin, string configJson, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var configType = plugin.ConfigType!;
        object? configObj;
#pragma warning disable CA1031 // 配置解析失败应归一化为业务错误而非抛出
        try
        {
            configObj = System.Text.Json.JsonSerializer.Deserialize(configJson, configType, _deserializeOptions);
        }
        catch (Exception ex)
        {
            return $"配置解析失败: {ex.Message}";
        }
#pragma warning restore CA1031

        var method = plugin.PluginType.GetMethod("InitAsync", new[] { configType });
        if (method == null)
        {
            return "未找到 InitAsync 方法";
        }

        var task = (Task)method.Invoke(instance, new[] { configObj })!;
        await task.ConfigureAwait(false);
        var error = GetTaskResult(task);
        return error as string;
    }

    private static async Task<object?> RunAsync(object instance, PluginInfo plugin, string requestJson, string? toolKey, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        Type requestType;
        MethodInfo method;
        if (plugin.IsMultiTool)
        {
            var tool = plugin.FindTool(toolKey);
            if (tool == null)
            {
                var available = string.Join("、", plugin.Tools.Select(x => x.Key));
                throw new BusinessException($"工具不存在：{toolKey}（可用工具：{available}）") { StatusCode = 400 };
            }

            requestType = tool.Request;
            method = tool.Method;
        }
        else
        {
            if (!string.IsNullOrWhiteSpace(toolKey) && !string.Equals(toolKey, "run", StringComparison.OrdinalIgnoreCase))
            {
                throw new BusinessException("该插件只有一个工具，无需指定工具 key") { StatusCode = 400 };
            }

            requestType = plugin.Request!;
            method = plugin.PluginType.GetMethod("RunAsync", new[] { requestType, typeof(CancellationToken) })
                ?? throw new BusinessException("未找到 RunAsync 方法") { StatusCode = 400 };
        }

        object? requestObj;
#pragma warning disable CA1031 // 请求参数解析失败应归一化为业务错误而非抛出
        try
        {
            requestObj = System.Text.Json.JsonSerializer.Deserialize(requestJson, requestType, _deserializeOptions);
        }
        catch (Exception ex)
        {
            throw new BusinessException($"请求参数解析失败: {ex.Message}") { StatusCode = 400 };
        }
#pragma warning restore CA1031

        var task = (Task)method.Invoke(instance, new[] { requestObj, cancellationToken })!;
        await task.ConfigureAwait(false);
        return GetTaskResult(task);
    }

    private static object? GetTaskResult(Task task)
    {
        var resultProperty = task.GetType().GetProperty("Result");
        return resultProperty?.GetValue(task);
    }

    private static async Task DisposeAsync(object instance)
    {
        if (instance is IAsyncDisposable asyncDisposable)
        {
            await asyncDisposable.DisposeAsync().ConfigureAwait(false);
        }
        else if (instance is IDisposable disposable)
        {
            disposable.Dispose();
        }
    }

    private static PluginRunResult Fail(PluginInfo plugin, string message, Exception? exception)
    {
        var inner = Unwrap(exception);
        var error = string.IsNullOrWhiteSpace(inner?.Message) ? message : $"{message}: {inner!.Message}";
        return new PluginRunResult { Key = plugin.Key, Success = false, Error = error };
    }

    private static Exception? Unwrap(Exception? exception)
    {
        if (exception is TargetInvocationException { InnerException: { } inner })
        {
            return inner;
        }

        return exception;
    }
}
