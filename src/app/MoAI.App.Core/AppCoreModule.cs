using Maomi;
using MoAI.App.Chat;
using MoAI.App.Workflow;

namespace MoAI.App;

/// <summary>
/// 应用模块核心层.
/// </summary>
[InjectModule<AppSharedModule>]
[InjectModule<AppApiModule>]
[InjectModule<AppChatAppCoreModule>]
[InjectModule<WorkflowCoreModule>]
public class AppCoreModule : IModule
{
    /// <inheritdoc/>
    public void ConfigureServices(ServiceContext context)
    {
    }
}
