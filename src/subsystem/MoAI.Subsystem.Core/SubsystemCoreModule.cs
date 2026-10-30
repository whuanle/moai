using Maomi;

namespace MoAI.Subsystem;

/// <summary>
/// SubsystemCoreModule.
/// </summary>
[InjectModule<SubsystemSharedModule>]
[InjectModule<SubsystemApiModule>]
public class SubsystemCoreModule : IModule
{
    /// <inheritdoc/>
    public void ConfigureServices(ServiceContext context)
    {
    }
}
