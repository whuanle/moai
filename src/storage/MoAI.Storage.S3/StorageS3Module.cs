using Maomi;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MoAI.Infra;
using MoAI.Storage.Services;
using MoAI.Store.Services;

namespace MoAI.Storage;

public class StorageS3Module : IModule
{
    public void ConfigureServices(ServiceContext context)
    {
        var systemOptions = context.Configuration.Get<SystemOptions>();
        if ("S3".Equals(systemOptions!.Storage.Type, StringComparison.OrdinalIgnoreCase))
        {
            context.Services.AddScoped<IPrivateFileStorage, S3PrivateStorage>();
            context.Services.AddScoped<IPublicFileStorage, S3PublicStorage>();
        }
    }
}
