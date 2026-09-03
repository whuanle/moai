using System.Threading;
using System.Threading.Tasks;
using MoAI.AIPlugin.Attributes;
using MoAI.AIPlugin.Contracts;
using MoAI.AIPlugin.Dynamic.Models;

namespace MoAI.AIPlugin.Dynamic.Plugins;

/// <summary>
/// 动态示例插件：按配置的前缀向目标打招呼，配置独立于每次请求.
/// </summary>
[AiPlugin(key: "dynamic_greet", Name = "动态问候", Description = "使用配置中的前缀打招呼")]
public class DynamicGreetPlugin : IDynamicPluginRuntime<DynamicGreetRequest, DynamicGreetResponse, DynamicGreetConfig>
{
    private DynamicGreetConfig? _config;

    /// <inheritdoc/>
    public static string GetParamsExampleValue()
    {
        return """
            {
            "Name":"MoAI" // 名称
            }
            """;
    }

    /// <inheritdoc/>
    public static string GetConfigExampleValue()
    {
        return """{"Prefix":"Hello"}""";
    }

    /// <inheritdoc/>
    public Task<string?> InitAsync(DynamicGreetConfig config)
    {
        if (string.IsNullOrWhiteSpace(config.Prefix))
        {
            return Task.FromResult<string?>("Prefix 不能为空");
        }

        _config = config;
        return Task.FromResult<string?>(null);
    }

    /// <inheritdoc/>
    public Task<DynamicGreetResponse> RunAsync(DynamicGreetRequest request, CancellationToken cancellationToken)
    {
        return Task.FromResult(new DynamicGreetResponse { Message = $"{_config?.Prefix} {request.Name}" });
    }
}
