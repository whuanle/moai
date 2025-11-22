#pragma warning disable CA1822 // 将成员标记为 static
#pragma warning disable CA1031 // 不捕获常规异常类型

using Maomi;
using Microsoft.SemanticKernel;
using MoAI.Infra.Exceptions;
using MoAI.Infra.System.Text.Json;
using MoAI.Plugin.Models;
using System.ComponentModel;

namespace MoAI.Plugin.Plugins.CurrentTime;

/// <summary>
/// 获取当前时间插件.
/// </summary>
[Attributes.NativePluginFieldConfig(
    "current_time",
    Name = "获取当前时间",
    Description = "获取当前系统时间",
    Classify = NativePluginClassify.Tool)]
[Description("获取当前系统时间")]
[InjectOnTransient]
public class CurrentTimePlugin : IToolPluginRuntime
{
    /// <inheritdoc/>
    public async Task<string> GetParamsExampleValue()
    {
        await Task.CompletedTask;

        // 此插件无需参数，返回空对象
        var example = new { };

        return System.Text.Json.JsonSerializer.Serialize(example, JsonSerializerOptionValues.UnsafeRelaxedJsonEscaping);
    }

    /// <summary>
    /// 获取当前系统时间.
    /// </summary>
    /// <returns>当前时间的字符串表示</returns>
    [KernelFunction("invoke")]
    [Description("获取当前系统时间")]
    public async Task<string> InvokeAsync()
    {
        await Task.CompletedTask;
        var currentTime = DateTimeOffset.Now.ToString();
        return $"当前时间为: {currentTime}";
    }

    /// <inheritdoc/>
    public async Task<string> TestAsync(string @params)
    {
        try
        {
            // 此插件无需参数，直接调用 InvokeAsync
            var result = await InvokeAsync();
            return System.Text.Json.JsonSerializer.Serialize(result, JsonSerializerOptionValues.UnsafeRelaxedJsonEscaping);
        }
        catch (Exception ex)
        {
            throw new BusinessException(ex.Message);
        }
    }
}