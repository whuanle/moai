using Maomi;
using Microsoft.Extensions.DependencyInjection;
using MoAI.Team.Services;

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
        context.Services.AddScoped<ITeamService, TeamService>();
    }
}
