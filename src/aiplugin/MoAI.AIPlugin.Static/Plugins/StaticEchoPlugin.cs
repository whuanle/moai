using System.Threading;
using System.Threading.Tasks;
using MoAI.AiPlugin.Attributes;
using MoAI.AiPlugin.Contracts;
using MoAI.AIPlugin.Static.Models;

namespace MoAI.AIPlugin.Static.Plugins;

/// <summary>
/// 静态示例插件：回显请求参数.
/// </summary>
[AiPlugin(key: "static_echo", Name = "静态回显", Description = "回显请求参数，无需额外配置")]
public class StaticEchoPlugin : IStaticPluginRuntime<StaticEchoRequest, StaticEchoResponse>
{
    /// <inheritdoc/>
    public static string GetParamsExampleValue()
    {
        return """{"Message":"hello"}""";
    }

    /// <inheritdoc/>
    public Task<StaticEchoResponse> RunAsync(StaticEchoRequest request, CancellationToken cancellationToken)
    {
        return Task.FromResult(new StaticEchoResponse { Message = $"echo:{request.Message}" });
    }
}
