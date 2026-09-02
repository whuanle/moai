using System.Threading;
using System.Threading.Tasks;
using MoAI.AiPlugin.Models;

namespace MoAI.AiPlugin.Services;

/// <summary>
/// 插件执行引擎。负责在隔离的 DI 作用域中实例化插件、应用配置并调用运行方法.
/// </summary>
public interface IPluginExecutor
{
    /// <summary>
    /// 按插件元数据执行插件.
    /// </summary>
    /// <param name="plugin">插件元数据.</param>
    /// <param name="requestJson">请求参数 JSON.</param>
    /// <param name="configJson">动态插件配置 JSON，静态插件忽略.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>执行结果.</returns>
    Task<PluginRunResult> ExecuteAsync(PluginInfo plugin, string requestJson, string? configJson, CancellationToken cancellationToken);

    /// <summary>
    /// 按插件 key 执行插件.
    /// </summary>
    /// <param name="key">插件 key.</param>
    /// <param name="requestJson">请求参数 JSON.</param>
    /// <param name="configJson">动态插件配置 JSON，静态插件忽略.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>执行结果.</returns>
    Task<PluginRunResult> ExecuteAsync(string key, string requestJson, string? configJson, CancellationToken cancellationToken);
}
