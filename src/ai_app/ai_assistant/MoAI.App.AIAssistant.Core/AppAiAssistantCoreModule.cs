using Maomi;
using Microsoft.Extensions.DependencyInjection;
using MoAI.App.AIAssistant.Core.Services;
using MoAI.App.AIAssistant.Services;

namespace MoAI.App.AIAssistant;

/// <summary>
/// AppAiAssistantCoreModule.
/// </summary>
[InjectModule<AiAssistantSharedModule>]
[InjectModule<AppAiAssistantApiModule>]
public class AppAiAssistantCoreModule : IModule
{
    /// <inheritdoc/>
    public void ConfigureServices(ServiceContext context)
    {
        context.Services.AddScoped<IChatHistoryCacheService, ChatHistoryCacheService>();
    }
}