using Maomi;
using Microsoft.Extensions.DependencyInjection;
using MoAI.Hangfire;

namespace MoAI.Plugin;

/*
* 自定义插件
* 内置插件
* 工具
 */

/// <summary>
/// PluginCoreModule.
/// </summary>
[InjectModule<PlugnSharedModule>]
[InjectModule<PluginApiModule>]
[InjectModule<NativePluginModule>]
[InjectModule<ToolPluginModule>]
public class PluginCoreModule : IModule
{
    /// <inheritdoc/>
    public void ConfigureServices(ServiceContext context)
    {
        context.Services.AddHostedService<AutoRegisterToolBackgroundService>();
    }
}
