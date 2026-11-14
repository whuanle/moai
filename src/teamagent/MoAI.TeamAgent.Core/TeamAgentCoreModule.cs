using Maomi;

namespace MoAI.TeamAgent;

/// <summary>
/// TeamAgentCoreModule.
/// </summary>
[InjectModule<TeamAgentSharedModule>]
[InjectModule<TeamAgentApiModule>]
public class TeamAgentCoreModule : IModule
{
    /// <inheritdoc/>
    public void ConfigureServices(ServiceContext context)
    {
    }
}
