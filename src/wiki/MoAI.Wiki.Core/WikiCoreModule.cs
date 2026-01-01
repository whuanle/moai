using Maomi;

namespace MoAI.Wiki;

/// <summary>
/// WikiCodeModule.
/// </summary>
[InjectModule<WikiSharedModule>]
[InjectModule<WikiApiModule>]
public class WikiCoreModule : IModule
{
    /// <inheritdoc/>
    public void ConfigureServices(ServiceContext context)
    {
    }
}
