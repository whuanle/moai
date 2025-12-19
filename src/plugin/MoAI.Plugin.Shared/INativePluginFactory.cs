using MoAI.Plugin.NativePlugins.Queries.Responses;

namespace MoAI.Plugin;

/// <summary>
/// 插件工厂.
/// </summary>
public interface INativePluginFactory
{
    /// <summary>
    /// 获取插件.
    /// </summary>
    /// <returns></returns>
    IReadOnlyCollection<NativePluginTemplateInfo> GetPlugins();

    /// <summary>
    /// 获取插件.
    /// </summary>
    /// <param name="key"></param>
    /// <returns></returns>
    NativePluginTemplateInfo? GetPluginByKey(string key);
}