using Maomi;

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
    }
}