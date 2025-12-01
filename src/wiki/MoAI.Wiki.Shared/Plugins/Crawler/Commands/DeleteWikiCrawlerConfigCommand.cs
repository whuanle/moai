using MediatR;
using MoAI.Infra.Models;

namespace MoAI.Wiki.Plugins.WikiCrawler.Commands;

/// <summary>
/// 删除配置.
/// </summary>
public class DeleteWikiCrawlerConfigCommand : IRequest<EmptyCommandResponse>
{
    /// <summary>
    /// 知识库 id.
    /// </summary>
    public int WikiId { get; init; }

    /// <summary>
    /// 配置.
    /// </summary>
    public int ConfigId { get; init; }

    /// <summary>
    /// 是否删除该配置下的所有网页.
    /// </summary>
    public bool DeleteDocuments { get; set; }
}