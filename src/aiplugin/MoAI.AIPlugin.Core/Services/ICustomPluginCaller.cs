using System.Threading;
using System.Threading.Tasks;
using MoAI.AIPlugin.Models;

namespace MoAI.AIPlugin.Services;

/// <summary>
/// 自定义插件（MCP/OpenAPI）调用端口：按插件名称执行插件内的指定函数.
/// <para>自定义插件存在于数据库（plugin + plugin_custom + plugin_functions），不在内存注册表中，
/// 由宿主模块（Custom）提供实现.</para>
/// </summary>
public interface ICustomPluginCaller
{
    /// <summary>
    /// 按插件名称执行自定义插件函数.
    /// </summary>
    /// <param name="pluginName">插件名称（plugin.plugin_name）.</param>
    /// <param name="functionName">函数名称；为空时仅插件只有一个函数才可执行.</param>
    /// <param name="requestJson">请求参数 JSON.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>执行结果；插件不存在或不是自定义插件（MCP/OpenAPI）时返回 null.</returns>
    Task<PluginRunResult?> RunAsync(string pluginName, string? functionName, string requestJson, CancellationToken cancellationToken);
}
