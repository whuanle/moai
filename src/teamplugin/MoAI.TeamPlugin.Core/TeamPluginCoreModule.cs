using Maomi;
using Microsoft.Extensions.DependencyInjection;

namespace MoAI.TeamPlugin;

/// <summary>
/// TeamPlugin 核心层模块。承载团队插件（创建后归团队）业务逻辑.
/// </summary>
[InjectModule<TeamPluginSharedModule>]
[InjectModule<TeamPluginApiModule>]
public class TeamPluginCoreModule : IModule
{
    /// <inheritdoc/>
    public void ConfigureServices(ServiceContext context)
    {
    }
}
