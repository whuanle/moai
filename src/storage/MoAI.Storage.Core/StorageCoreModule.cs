using Maomi;
using Microsoft.Extensions.DependencyInjection;

namespace MoAI.Storage;

/// <summary>
/// StorageCoreModule.
/// </summary>
[InjectModule<StorageS3Module>]
[InjectModule<StorageLocalModule>]
[InjectModule<StorageApiModule>]
public class StorageCoreModule : IModule
{
    /// <inheritdoc/>
    public void ConfigureServices(ServiceContext context)
    {
        context.Services.AddScoped<ContentMiddleware>();
    }
}
