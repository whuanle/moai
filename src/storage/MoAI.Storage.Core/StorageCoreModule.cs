using Maomi;

namespace MoAI.Storage;

/// <summary>
/// StorageCoreModule.
/// </summary>
[InjectModule<StorageApiModule>]
public class StorageCoreModule : IModule
{
    /// <inheritdoc/>
    public void ConfigureServices(ServiceContext context)
    {
    }
}
