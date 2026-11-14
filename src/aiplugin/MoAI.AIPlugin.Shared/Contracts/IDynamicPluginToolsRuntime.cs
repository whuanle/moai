using System.Threading;
using System.Threading.Tasks;

namespace MoAI.AIPlugin.Contracts;

/// <summary>
/// 多工具动态插件运行时接口：一个模板类经多个 <see cref="Attributes.AiPluginToolAttribute"/> 标记的方法对外暴露多个工具.
/// 配置模型与单工具动态插件一致（<see cref="InitAsync"/> 校验 + <see cref="GetConfigExampleValue"/> 示例），
/// 执行时按工具 key 分发到对应方法；存量单工具插件（<see cref="IDynamicPluginRuntime{,,}"/>）不受影响.
/// </summary>
/// <typeparam name="TConfig">配置类型.</typeparam>
public interface IDynamicPluginToolsRuntime<TConfig>
{
    /// <summary>
    /// 获取配置示例 JSON 字符串.
    /// </summary>
    /// <returns>示例 JSON 字符串.</returns>
    static abstract string GetConfigExampleValue();

    /// <summary>
    /// 初始化或校验配置。返回非空字符串表示配置校验失败（错误信息）.
    /// </summary>
    /// <param name="config">配置对象.</param>
    /// <returns>返回 null 表示校验通过，否则返回错误信息.</returns>
    Task<string?> InitAsync(TConfig config);
}
