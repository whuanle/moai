using Maomi;

namespace MoAI.AIPlugin.Custom;

/// <summary>
/// 自定义插件宿主模块。该程序集用于承载用户自定义 / 三方插件，可按需新增插件实现.
/// </summary>
public class CustomPluginsModule : IModule
{
    /// <inheritdoc/>
    public void ConfigureServices(ServiceContext context)
    {
    }
}
