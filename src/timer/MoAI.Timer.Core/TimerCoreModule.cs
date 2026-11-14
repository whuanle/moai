using Maomi;
using Microsoft.Extensions.DependencyInjection;
using MoAI.Timer.Services;

namespace MoAI.Timer;

/// <summary>
/// TimerCoreModule.
/// </summary>
[InjectModule<TimerSharedModule>]
[InjectModule<TimerApiModule>]
public class TimerCoreModule : IModule
{
    /// <inheritdoc/>
    public void ConfigureServices(ServiceContext context)
    {
        context.Services.AddHostedService<TeamTimerStaleExecutionSweeper>();
    }
}
