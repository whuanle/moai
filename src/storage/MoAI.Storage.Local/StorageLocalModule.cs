using Maomi;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MoAI.Infra;
using MoAI.Storage.Services;
using MoAI.Store.Services;

namespace MoAI.Storage;

public class StorageLocalModule : IModule
{
    public void ConfigureServices(ServiceContext context)
    {
        var systemOptions = context.Configuration.Get<SystemOptions>();
        if ("local".Equals(systemOptions!.Storage.Type, StringComparison.OrdinalIgnoreCase))
        {
            context.Services.AddScoped<IPrivateFileStorage, LocalPrivateStorage>();
            context.Services.AddScoped<IPublicFileStorage, LocalPubliceStorage>();
        }
    }
}
