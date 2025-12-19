#pragma warning disable CA1822 // 将成员标记为 static

namespace MoAI.Plugin.Plugins;

/// <summary>
/// 内置插件接口.
/// </summary>
public interface INativePluginRuntime : IToolPluginRuntime
{
    /// <summary>
    /// 检查字符串 json 配置，返回错误信息.
    /// </summary>
    /// <param name="config"></param>
    /// <returns></returns>
    Task<string?> CheckConfigAsync(string config);

    /// <summary>
    /// 初始化配置.
    /// </summary>
    /// <param name="config"></param>
    /// <returns></returns>
    Task ImportConfigAsync(string config);
}
