using MediatR;
using MoAI.Infra.Models;

namespace MoAI.Wiki.Plugins.WikiCrawler.Queries;

/// <summary>
/// 查询知识库爬虫列表.
/// </summary>
public class QueryWikiCrawlerConfigListCommand : IRequest<QueryWikiCrawlerConfigListCommandResponse>
{
    /// <summary>
    /// 知识库 id.
    /// </summary>
    public int WikiId { get; init; }
}

public class QueryWikiCrawlerConfigListCommandResponse
{
    /// <summary>
    /// 爬虫配置列表.
    /// </summary>
    public IReadOnlyCollection<WikiWebConfigSimpleItem> Items { get; init; } = Array.Empty<WikiWebConfigSimpleItem>();
}

public class WikiWebConfigSimpleItem : AuditsInfo
{
    /// <summary>
    /// 爬虫配置 id.
    /// </summary>
    public int Id { get; init; }

    /// <summary>
    /// 知识库 id.
    /// </summary>
    public int WikiId { get; init; }

    /// <summary>
    /// 爬虫标题.
    /// </summary>
    public string Title { get; init; } = default!;

    /// <summary>
    /// 爬虫地址.
    /// </summary>
    public string Address { get; init; } = default!;
}