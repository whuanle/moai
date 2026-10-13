using Maomi;

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
    }
}
