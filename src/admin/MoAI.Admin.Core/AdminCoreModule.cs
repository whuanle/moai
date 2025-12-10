using Maomi;

namespace MoAI.Admin;

/// <summary>
/// AdminCoreModule.
/// </summary>
[InjectModule<AdminSharedModel>]
[InjectModule<AdminApiModule>]
public class AdminCoreModule : IModule
{
    /// <inheritdoc/>
    public void ConfigureServices(ServiceContext context)
    {
    }
}
