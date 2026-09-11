using System.Threading;
using System.Threading.Tasks;
using MoAI.AIPlugin.Attributes;
using MoAI.AIPlugin.Contracts;
using MoAI.AIPlugin.Static.Models;
using MoAI.Infra.Exceptions;

namespace MoAI.AIPlugin.Static.Plugins;

/// <summary>
/// 流程等待（静态插件）：等待指定秒数后继续执行.
/// </summary>
[AiPlugin(key: "static_flow_wait", Name = "流程等待", Description = "让工作流等待指定时间后运行")]
public class FlowWaitPlugin : IStaticPluginRuntime<FlowWaitRequest, FlowWaitResponse>
{
    /// <summary>
    /// 毫秒与秒的换算系数.
    /// </summary>
    private const int MillisecondsPerSecond = 1000;

    /// <inheritdoc/>
    public static string GetParamsExampleValue()
    {
        return """
            {
            "WaitTimeInSeconds": 10 // 等待时间（秒）
            }
            """;
    }

    /// <inheritdoc/>
    public async Task<FlowWaitResponse> RunAsync(FlowWaitRequest request, CancellationToken cancellationToken)
    {
        if (request.WaitTimeInSeconds < 0)
        {
            throw new BusinessException(400, "等待时间不能为负数");
        }

        await Task.Delay(request.WaitTimeInSeconds * MillisecondsPerSecond, cancellationToken).ConfigureAwait(false);
        return new FlowWaitResponse { Message = $"已等待 {request.WaitTimeInSeconds} 秒" };
    }
}
