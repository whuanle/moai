using Maomi;
using Microsoft.Extensions.DependencyInjection;
using MoAI.Prompt.Services;
using MoAI.Statistics.Services;

namespace MoAI.Prompt;

/// <summary>
/// PromptCoreModule.
/// </summary>
[InjectModule<PromptSharedModule>]
[InjectModule<PromptApiModule>]
public class PromptCoreModule : IModule
{
    /// <inheritdoc/>
    public void ConfigureServices(ServiceContext context)
    {
        // 提示词使用次数计数器：Redis 计数 + 统计模块每分钟批量累加到 prompts.use_count
        context.Services.AddScoped<IPromptUsageCounter, PromptUsageCounter>();
        context.Services.AddScoped<IResourceUsageWriter, PromptUsageCounterActivatorJob>();
    }
}
