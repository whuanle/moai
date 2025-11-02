using Maomi;

namespace MoAI.Plugin;

/// <summary>
/// PluginCoreModule.
/// </summary>
[InjectModule<PluginApiModule>]
[InjectModule<PluginBuildInModule>]
public class PluginCoreModule : IModule
{
    /// <inheritdoc/>
    public void ConfigureServices(ServiceContext context)
    {
    }
}
