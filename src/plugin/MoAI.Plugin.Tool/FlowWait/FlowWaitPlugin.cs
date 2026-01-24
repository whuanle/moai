#pragma warning disable CA1822 // 将成员标记为 static
#pragma warning disable CA1031 // 不捕获常规异常类型

using Maomi;
using Microsoft.SemanticKernel;
using MoAI.Infra.Exceptions;
using MoAI.Infra.System.Text.Json;
using MoAI.Plugin.Models;
using System.ComponentModel;

namespace MoAI.Plugin.Plugins.FlowWait;

/// <summary>
/// 流程等待插件.
/// </summary>
[Attributes.NativePluginConfig(
    "flow_wait",
    Name = "流程等待",
    Description = "让工作流等待指定时间后运行",
    Classify = NativePluginClassify.Tool,
    ParamType = typeof(FlowWaitParams))]
[Description("让工作流等待指定时间后运行")]
[InjectOnTransient]
public class FlowWaitPlugin : IToolPluginRuntime
{
    /// <inheritdoc/>
    public static string GetParamsExampleValue()
    {
        var example = new FlowWaitParams
        {
            WaitTimeInSeconds = 10 // 示例：等待 10 秒
        };

        return System.Text.Json.JsonSerializer.Serialize(example, JsonSerializerOptionValues.UnsafeRelaxedJsonEscaping);
    }

    /// <summary>
    /// 等待指定时间后继续执行.
    /// </summary>
    /// <param name="waitTimeInSeconds">等待时间（秒）</param>
    /// <returns></returns>
    [KernelFunction("invoke")]
    [Description("等待指定时间后继续执行")]
    public async Task<string> InvokeAsync([Description("等待时间（秒）")] int waitTimeInSeconds)
    {
        if (waitTimeInSeconds < 0)
        {
            throw new BusinessException("等待时间不能为负数");
        }

        await Task.Delay(waitTimeInSeconds * 1000); // 转换为毫秒
        return $"已等待 {waitTimeInSeconds} 秒";
    }

    /// <inheritdoc/>
    public async Task<string> TestAsync(string @params)
    {
        try
        {
            var input = System.Text.Json.JsonSerializer.Deserialize<FlowWaitParams>(@params, JsonSerializerOptionValues.UnsafeRelaxedJsonEscaping);
            var result = await InvokeAsync(input!.WaitTimeInSeconds);
            return System.Text.Json.JsonSerializer.Serialize(result, JsonSerializerOptionValues.UnsafeRelaxedJsonEscaping);
        }
        catch (Exception ex)
        {
            throw new BusinessException(ex.Message);
        }
    }
}
