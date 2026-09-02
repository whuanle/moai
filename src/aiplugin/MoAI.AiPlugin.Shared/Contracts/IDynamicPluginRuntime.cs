using System.Threading;
using System.Threading.Tasks;

namespace MoAI.AiPlugin.Contracts;

/// <summary>
/// 动态插件运行时接口。动态插件存在独立于请求之外的配置，运行前需先调用 <see cref="InitAsync"/> 校验并应用配置.
/// </summary>
/// <typeparam name="TRequest">请求参数类型.</typeparam>
/// <typeparam name="TResponse">响应结果类型.</typeparam>
/// <typeparam name="TConfig">配置类型.</typeparam>
public interface IDynamicPluginRuntime<TRequest, TResponse, TConfig> : IPluginRuntime<TRequest, TResponse>
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
