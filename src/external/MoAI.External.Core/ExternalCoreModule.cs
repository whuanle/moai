using Maomi;

namespace MoAI.External;

/// <summary>
/// External Core 模块.
/// </summary>
[InjectModule<ExternalSharedModule>]
[InjectModule<ExternalApiModule>]
public class ExternalCoreModule : IModule
{
    /// <inheritdoc/>
    public void ConfigureServices(ServiceContext context)
    {
    }
}
