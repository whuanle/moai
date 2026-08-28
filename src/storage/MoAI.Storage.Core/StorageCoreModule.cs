using Maomi;

namespace MoAI.Storage;

/// <summary>
/// StorageCoreModule.
/// </summary>
[InjectModule<StorageSharedModule>]
[InjectModule<StorageS3Module>]
public class StorageCoreModule : IModule
{
    /// <inheritdoc/>
    public void ConfigureServices(ServiceContext context)
    {
    }
}
