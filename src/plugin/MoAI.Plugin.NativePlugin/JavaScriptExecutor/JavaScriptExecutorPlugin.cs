#pragma warning disable CA1822 // 将成员标记为 static
#pragma warning disable CA1031 // 不捕获常规异常类型

using Jint;
using Maomi;
using Microsoft.SemanticKernel;
using MoAI.Infra.Exceptions;
using MoAI.Infra.System.Text.Json;
using MoAI.Plugin.Models;
using MoAI.Plugin.Plugins;
using System.ComponentModel;
using System.Text.Json;

namespace MoAI.Plugin.JavaScriptExecutor;

/// <summary>
/// JavaScript 执行插件.
/// </summary>
[Attributes.NativePluginConfig(
    "javascript_executor",
    Name = "JavaScript 执行器",
    Description = "执行指定的 JavaScript 代码，限制最大内存4MB、超时4s、最大语句1000行",
    Classify = NativePluginClassify.Tool,
    ConfigType = typeof(JavaScriptExecutorConfig))]
[Description("执行指定的 JavaScript 代码，限制最大内存4MB、超时4s、最大语句1000行")]
[InjectOnTransient]
public class JavaScriptExecutorPlugin : INativePluginRuntime
{
    private JavaScriptExecutorConfig _config = default!;

    /// <inheritdoc/>
    public static string GetParamsExampleValue()
    {
        var example = new
        {
            id = 1,
            name = "test"
        };

        return System.Text.Json.JsonSerializer.Serialize(example, JsonSerializerOptionValues.UnsafeRelaxedJsonEscaping);
    }

    /// <summary>
    /// 执行 JavaScript 代码.
    /// </summary>
    /// <param name="paramter"></param>
    /// <returns>执行结果的 JSON 字符串</returns>
    [KernelFunction("invoke")]
    [Description("执行指定的 JavaScript 代码")]
    public async Task<string> InvokeAsync([Description("JavaScript 代码的参数,json 字符串对象，示例：'{\"id\":1,name:\"test\"}'")] string paramter)
    {
        try
        {
            await Task.CompletedTask;
            using var engine = new Engine(options =>
            {
                options.LimitMemory(4_000_000); // 限制内存为 4 MB
                options.LimitRecursion(100); // 限制递归深度为 100
                options.TimeoutInterval(TimeSpan.FromSeconds(4)); // 超时时间为 4 秒
                options.MaxStatements(1000); // 最大执行语句数为 1000
            });

            var result = engine.Execute(_config.JavaScriptCode)
                .Invoke("run", paramter);

            if (result.IsNull() || result.IsUndefined())
            {
                return "无执行结果或代码错误，请检查 javascript 代码";
            }

            var dotNetResult = result.ToObject();

            // 不设置自定义配置，保留原样输出
            var serializedResult = JsonSerializer.Serialize(dotNetResult);
            return serializedResult;
        }
        catch (Exception ex)
        {
            throw new BusinessException($"JavaScript 执行失败: {ex.Message}");
        }
    }

    /// <inheritdoc/>
    public async Task<string> TestAsync(string @params)
    {
        return await InvokeAsync(@params);
    }

    /// <inheritdoc/>
    public async Task<string?> CheckConfigAsync(string config)
    {
        await Task.CompletedTask;
        try
        {
            var objectParams = JsonSerializer.Deserialize<JavaScriptExecutorConfig>(config, JsonSerializerOptionValues.UnsafeRelaxedJsonEscaping);
            if (string.IsNullOrWhiteSpace(objectParams?.JavaScriptCode))
            {
                return "JavaScript 代码不能为空";
            }

            return string.Empty;
        }
        catch (Exception ex)
        {
            return $"JavaScript 代码解析失败: {ex.Message}";
        }
    }

    /// <inheritdoc/>
    public async Task ImportConfigAsync(string config)
    {
        await Task.CompletedTask;
        _config = JsonSerializer.Deserialize<JavaScriptExecutorConfig>(config)!;
    }
}
