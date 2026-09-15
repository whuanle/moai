using Maomi;

namespace MoAI.Publication;

/// <summary>
/// PublicationCoreModule.
/// </summary>
[InjectModule<PublicationSharedModule>]
[InjectModule<PublicationApiModule>]
public class PublicationCoreModule : IModule
{
    /// <inheritdoc/>
    public void ConfigureServices(ServiceContext context)
    {
    }
}
