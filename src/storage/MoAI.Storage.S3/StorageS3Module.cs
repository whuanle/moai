using Maomi;
using Microsoft.Extensions.DependencyInjection;
using MoAI.Storage.Middlewares;
using MoAI.Storage.Services;

namespace MoAI.Storage;

/// <summary>
/// StorageS3Module.
/// </summary>
[InjectModule<StorageSharedModule>]
public class StorageS3Module : IModule
{
    /// <inheritdoc/>
    public void ConfigureServices(ServiceContext context)
    {
        // S3 客户端为线程安全，可全局复用
        context.Services.AddSingleton<S3Client>();
        context.Services.AddScoped<IStorageService, StorageService>();
        context.Services.AddScoped<StorageStaticFilesMiddleware>();
    }
}
