using System.Threading;
using System.Threading.Tasks;
using MoAI.AIPlugin.Attributes;
using MoAI.AIPlugin.Contracts;
using MoAI.AIPlugin.Static.Models;

namespace MoAI.AIPlugin.Static.Plugins;

/// <summary>
/// 静态示例插件：回显请求参数.
/// </summary>
[AiPlugin(key: "static_echo", Name = "静态回显", HumanDescription = "用于原样返回输入内容，方便检查参数是否正确传递。适合插件接入测试、工作流调试和功能演示，不会对输入做额外处理。", Description = "回显请求参数，无需额外配置")]
public class StaticEchoPlugin : IStaticPluginRuntime<StaticEchoRequest, StaticEchoResponse>
{
    /// <inheritdoc/>
    public static string GetParamsExampleValue()
    {
        return """
            {
            "Message":"hello"  // 要发送的消息
            }
            """;
    }

    /// <inheritdoc/>
    public Task<StaticEchoResponse> RunAsync(StaticEchoRequest request, CancellationToken cancellationToken)
    {
        return Task.FromResult(new StaticEchoResponse { Message = $"echo:{request.Message}" });
    }
}
