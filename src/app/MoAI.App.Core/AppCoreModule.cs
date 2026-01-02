using Maomi;

namespace MoAI.App;

/// <summary>
/// 应用模块核心层.
/// </summary>
[InjectModule<AppSharedModule>]
[InjectModule<AppApiModule>]
public class AppCoreModule : IModule
{
    /// <inheritdoc/>
    public void ConfigureServices(ServiceContext context)
    {
    }
}
