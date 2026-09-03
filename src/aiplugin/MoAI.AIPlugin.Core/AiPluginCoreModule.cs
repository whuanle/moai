using Maomi;
using Microsoft.Extensions.DependencyInjection;
using MoAI.AIPlugin.Services;

namespace MoAI.AIPlugin;

/// <summary>
/// AIPlugin 核心层模块。注册插件注册表与执行引擎.
/// </summary>
[InjectModule<AIPluginSharedModule>]
[InjectModule<AIPluginApiModule>]
public class AIPluginCoreModule : IModule
{
    /// <inheritdoc/>
    public void ConfigureServices(ServiceContext context)
    {
        context.Services.AddSingleton<IPluginRegistry, PluginRegistry>();
        context.Services.AddSingleton<IPluginExecutor, PluginExecutor>();
    }
}
