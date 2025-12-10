using Maomi;

namespace MoAI.Common;

/// <summary>
/// PublicCoreModule.
/// </summary>
[InjectModule<CommonSharedModule>]
[InjectModule<CommonApiModule>]
public class CommonCoreModule : IModule
{
    /// <inheritdoc/>
    public void ConfigureServices(ServiceContext context)
    {
    }
}