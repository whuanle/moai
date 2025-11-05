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
    /// <param name="config"></param>
    /// <returns></returns>
    Task<string?> CheckConfigAsync(string config);

    /// <summary>
    /// 初始化配置.
    /// </summary>
    /// <param name="config"></param>
    /// <returns></returns>
    Task ImportConfigAsync(string config);

    /// <summary>
    /// 获取参数示例值.
    /// </summary>
    /// <returns></returns>
    Task<string> GetParamsExampleValue();

    /// <summary>
    /// 使用参数运行测试.
    /// </summary>
    /// <param name="params"></param>
    /// <returns></returns>
    Task<string> TestAsync(string @params);
}
