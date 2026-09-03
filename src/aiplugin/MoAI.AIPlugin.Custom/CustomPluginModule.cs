using Maomi;
using Microsoft.Extensions.DependencyInjection;
using MoAI.AIPlugin;
using MoAI.AIPlugin.Services;

namespace MoAI.AIPlugin.Custom;

/// <summary>
/// 自定义插件宿主模块。承载用户自定义 / 三方插件的业务逻辑（Handler）.
/// </summary>
[InjectModule<AIPluginCoreModule>]
public class CustomPluginModule : IModule
{
    /// <inheritdoc/>
    public void ConfigureServices(ServiceContext context)
    {
        context.Services.AddScoped<IDynamicInstanceResolver, DynamicInstanceResolver>();
    }
}
