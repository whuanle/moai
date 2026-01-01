using Maomi;

namespace MoAI.Team;

/// <summary>
/// TeamCoreModule.
/// </summary>
[InjectModule<TeamSharedModule>]
[InjectModule<TeamApiModule>]
public class TeamCoreModule : IModule
{
    /// <inheritdoc/>
    public void ConfigureServices(ServiceContext context)
    {
    }
}
