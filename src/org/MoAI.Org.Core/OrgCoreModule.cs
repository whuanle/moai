using Maomi;
using Microsoft.Extensions.DependencyInjection;

namespace MoAI.Org;

/// <summary>
/// OrgCoreModule.
/// </summary>
[InjectModule<OrgSharedModule>]
[InjectModule<OrgApiModule>]
public class OrgCoreModule : IModule
{
    /// <inheritdoc/>
    public void ConfigureServices(ServiceContext context)
    {
    }
}
