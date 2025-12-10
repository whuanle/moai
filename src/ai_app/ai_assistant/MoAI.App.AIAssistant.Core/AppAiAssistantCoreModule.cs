using Maomi;

namespace MoAI.App.AIAssistant;

[InjectModule<AiAssistantSharedModule>]
[InjectModule<AppAiAssistantApiModule>]
public class AppAiAssistantCoreModule : IModule
{
    public void ConfigureServices(ServiceContext context)
    {
    }
}
