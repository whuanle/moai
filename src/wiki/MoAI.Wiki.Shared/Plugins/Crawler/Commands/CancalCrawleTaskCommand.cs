using MediatR;
using MoAI.Infra.Models;

namespace MoAI.Wiki.Plugins.WikiCrawler.Commands;

/// <summary>
/// 取消爬虫任务.
/// </summary>
public class CancalCrawleTaskCommand : IRequest<EmptyCommandResponse>
{
    /// <summary>
    /// 知识库 id.
    /// </summary>
    public int WikiId { get; init; }

    /// <summary>
    /// 配置.
    /// </summary>
    public int ConfigId { get; init; }
}
