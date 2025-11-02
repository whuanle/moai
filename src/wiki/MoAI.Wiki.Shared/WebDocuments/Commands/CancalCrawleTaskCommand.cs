using MediatR;
using MoAI.Infra.Models;

namespace MoAI.Wiki.WebDocuments.Commands;

/// <summary>
/// 取消爬虫任务.
/// </summary>
public class CancalCrawleTaskCommand : IRequest<EmptyCommandResponse>
{
    /// <summary>
    /// 知识库 id.
    /// </summary>
    public int WikiId { get; init; }

    public int WikiWebConfigId { get; init; }

    public Guid TaskId { get; init; }
}
