using MediatR;

namespace MoAI.Plugin.CustomPlugins.Queries;

/// <summary>
/// 获取插件的函数列表.
/// </summary>
public class QueryPluginFunctionsListCommand : IRequest<QueryPluginFunctionsListCommandResponse>
{
    /// <summary>
    /// 插件 id.
    /// </summary>
    public int PluginId { get; init; }
}
