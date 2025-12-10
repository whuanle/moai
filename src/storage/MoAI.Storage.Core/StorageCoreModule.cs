using Maomi;
using Microsoft.Extensions.DependencyInjection;
using MoAI.Infra;
using MoAI.Storage.Services;

namespace MoAI.Storage;

/// <summary>
/// StorageCoreModule.
/// </summary>
[InjectModule<StorageSharedModule>]
[InjectModule<StorageLocalModule>]
[InjectModule<StorageApiModule>]
public class StorageCoreModule : IModule
{
    private readonly SystemOptions _systemOptions;

    /// <summary>
    /// Initializes a new instance of the <see cref="StorageCoreModule"/> class.
    /// </summary>
    /// <param name="systemOptions"></param>
    public StorageCoreModule(SystemOptions systemOptions)
    {
        _systemOptions = systemOptions;
    }

    /// <inheritdoc/>
    public void ConfigureServices(ServiceContext context)
    {
        if (!string.IsNullOrEmpty(_systemOptions.Storage.Type) && "S3".Equals(_systemOptions!.Storage.Type, StringComparison.OrdinalIgnoreCase))
        {
            context.Services.AddScoped<IStorage, S3Storage>();
        }
        else
        {
            context.Services.AddScoped<IStorage, LocalStorage>();
        }
    }
}
