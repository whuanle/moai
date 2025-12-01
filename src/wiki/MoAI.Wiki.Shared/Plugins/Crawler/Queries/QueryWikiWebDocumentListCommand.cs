using MediatR;
using MoAI.Infra.Models;
using MoAI.Wiki.Wikis.Queries.Response;

namespace MoAI.Wiki.Plugins.WikiCrawler.Queries;

/// <summary>
/// 查询 web 文档列表.
/// </summary>
public class QueryWikiCrawlerListCommand : PagedParamter, IRequest<QueryWikiDocumentListCommandResponse>
{
    /// <summary>
    /// 知识库 id.
    /// </summary>
    public int WikiId { get; init; }

    /// <summary>
    /// web 爬虫 id.
    /// </summary>
    public int WikiCrawlerConfigId { get; init; }

    /// <summary>
    /// 筛选文件名称.
    /// </summary>
    public string? Query { get; init; } = default!;
}
