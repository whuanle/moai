using MediatR;
using MoAI.Infra.Models;
using MoAI.Wiki.Models;
using MoAI.Wiki.Plugins.Crawler.Queries.Responses;

namespace MoAI.Wiki.Plugins.Template.Models;
public class QueryWikiPluginrConfigCommandResponse<T> : AuditsInfo
        where T : IWikiPluginKey
{
    /// <summary>
    /// 知识库id.
    /// </summary>
    public int WikiId { get; init; }

    /// <summary>
    /// id.
    /// </summary>
    public int ConfigId { get; init; }

    /// <summary>
    /// 标题.
    /// </summary>
    public string Title { get; init; } = default!;

    /// <summary>
    /// 配置.
    /// </summary>
    public T Config { get; init; } = default!;
}