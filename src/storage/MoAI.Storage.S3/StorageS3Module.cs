using Maomi;
using Microsoft.Extensions.DependencyInjection;
using MoAI.Infra;
using MoAI.Storage.Services;

namespace MoAI.Storage;

/// <summary>
/// StorageS3Module.
/// </summary>
public class StorageS3Module : IModule
{
    private readonly SystemOptions _systemOptions;

    /// <summary>
    /// Initializes a new instance of the <see cref="StorageS3Module"/> class.
    /// </summary>
    /// <param name="systemOptions"></param>
    public StorageS3Module(SystemOptions systemOptions)
    {
        _systemOptions = systemOptions;
    }

    /// <inheritdoc/>
    public void ConfigureServices(ServiceContext context)
    {
        if ("S3".Equals(_systemOptions!.Storage.Type, StringComparison.OrdinalIgnoreCase))
        {
            context.Services.AddScoped<IStorage, S3Storage>();
        }
    }
}
