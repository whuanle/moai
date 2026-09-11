using Maomi;

namespace MoAI.App;

/// <summary>
/// AppCoreModule.
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
