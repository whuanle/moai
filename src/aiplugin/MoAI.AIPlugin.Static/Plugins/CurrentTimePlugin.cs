using System;
using System.Threading;
using System.Threading.Tasks;
using MoAI.AIPlugin.Attributes;
using MoAI.AIPlugin.Contracts;
using MoAI.AIPlugin.Static.Models;

namespace MoAI.AIPlugin.Static.Plugins;

/// <summary>
/// 获取当前时间（静态插件）：返回服务器当前系统时间.
/// </summary>
[AiPlugin(key: "static_current_time", Name = "获取当前时间", HumanDescription = "用于获取系统当前时间，帮助回答涉及今天、时段或时间先后的问题。适合日程提醒、时间判断和工作流排程，不需要额外配置时间服务。", Description = "获取当前系统时间，无需额外配置")]
public class CurrentTimePlugin : IStaticPluginRuntime<CurrentTimeRequest, CurrentTimeResponse>
{
    /// <inheritdoc/>
    public static string GetParamsExampleValue()
    {
        // 此插件无需参数，返回空对象
        return "{}";
    }

    /// <inheritdoc/>
    public Task<CurrentTimeResponse> RunAsync(CurrentTimeRequest request, CancellationToken cancellationToken)
    {
        return Task.FromResult(new CurrentTimeResponse
        {
            CurrentTime = DateTimeOffset.Now.ToString(),
        });
    }
}
