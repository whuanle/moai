#pragma warning disable CA1822 // 将成员标记为 static
#pragma warning disable CA1031 // 不捕获常规异常类型

using Jint;
using Maomi;
using Microsoft.SemanticKernel;
using MoAI.Infra.Exceptions;
using MoAI.Infra.System.Text.Json;
using MoAI.Plugin.Models;
using System.ComponentModel;
using System.Text.Json;
using System.Text.Json.Serialization;
using static Org.BouncyCastle.Math.EC.ECCurve;

namespace MoAI.Plugin.Plugins.JavaScriptExecutor;

/// <summary>
/// JavaScript 执行插件.
/// </summary>
[Attributes.NativePluginConfig(
    "javascript_executor_nohave_paramter",
    Name = "JavaScript 执行器",
    Description = "执行指定的 JavaScript 代码，限制最大内存4MB、超时4s、最大语句1000行",
    Classify = NativePluginClassify.Tool)]
[Description("执行指定的 JavaScript 代码，无输入参数")]
[InjectOnTransient]
public class JavaScriptExecutorPlugin : IToolPluginRuntime
{
    /// <inheritdoc/>
    public static string GetParamsExampleValue()
    {
        var code = """
            // 函数必须以 run 命名，paramter 参数是字符串
            // 返回值可以是对象或字符串等类型
            function run() {
            const result = {
                id: 666,
                name: "test"
            };
            
            return result;
            }
            """;

        return System.Text.Json.JsonSerializer.Serialize(code, JsonSerializerOptionValues.UnsafeRelaxedJsonEscaping);
    }

    /// <summary>
    /// 执行 JavaScript 代码.
    /// </summary>
    /// <param name="javaScriptCode">JavaScript 代码</param>
    /// <returns>执行结果的 JSON 字符串</returns>
    [KernelFunction("invoke")]
    [Description("执行指定的 JavaScript 代码")]
    public async Task<string> InvokeAsync([Description("JavaScript 代码")] string javaScriptCode)
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

            var result = engine.Execute(javaScriptCode)
                .Invoke("run");

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
}