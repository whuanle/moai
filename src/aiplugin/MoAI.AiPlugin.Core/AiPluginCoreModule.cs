using Maomi;
using Microsoft.Extensions.DependencyInjection;
using MoAI.AiPlugin.Services;

namespace MoAI.AiPlugin;

/// <summary>
/// AiPlugin 核心层模块。注册插件注册表与执行引擎.
/// </summary>
[InjectModule<AiPluginSharedModule>]
[InjectModule<AiPluginApiModule>]
public class AiPluginCoreModule : IModule
{
    /// <inheritdoc/>
    public void ConfigureServices(ServiceContext context)
    {
        context.Services.AddSingleton<IPluginRegistry, PluginRegistry>();
        context.Services.AddSingleton<IPluginExecutor, PluginExecutor>();
    }
}
