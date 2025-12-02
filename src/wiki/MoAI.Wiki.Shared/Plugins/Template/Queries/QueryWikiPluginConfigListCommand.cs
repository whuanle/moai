using MediatR;
using MoAI.Infra.Models;
using MoAI.Wiki.Plugins.Template.Models;

namespace MoAI.Wiki.Plugins.Template.Queries;

/// <summary>
/// 查询插件类型.
/// </summary>
public class QueryWikiPluginConfigListCommand : IRequest<QueryWikiPluginConfigListCommandResponse>
{
    /// <summary>
    /// 知识库 id.
    /// </summary>
    public int WikiId { get; init; }

    /// <summary>
    /// 筛选插件类型.
    /// </summary>
    public string? PluginKey { get; init; }
}
