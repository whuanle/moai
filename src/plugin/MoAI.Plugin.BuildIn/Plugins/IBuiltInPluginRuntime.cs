#pragma warning disable CA1822 // 将成员标记为 static

using MoAI.Infra.Models;

namespace MoAI.Plugin.Plugins;

/// <summary>
/// 插件运行时.
/// </summary>
public interface IInternalPluginRuntime
{
    /// <summary>
    /// 导出配置格式，获取这个插件需要什么参数.
    /// </summary>
    /// <returns></returns>
    Task<IReadOnlyCollection<InternalPluginParamConfig>> ExportConfigAsync();

    /// <summary>
    /// 检查配置，返回是否有问题.
    /// </summary>
    /// <param name="params"></param>
    /// <returns></returns>
    Task<IReadOnlyCollection<KeyValueString>> CheckConfigAsync(string @params);

    /// <summary>
    /// 初始化配置.
    /// </summary>
    /// <param name="json"></param>
    /// <returns></returns>
    Task ImportConfigAsync(string json);
}
