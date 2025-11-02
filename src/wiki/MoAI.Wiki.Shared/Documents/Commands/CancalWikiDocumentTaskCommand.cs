using MediatR;
using MoAI.Infra.Models;

namespace MoAI.Wiki.Documents.Commands;

/// <summary>
/// 取消文档处理任务.
/// </summary>
public class CancalWikiDocumentTaskCommand : IRequest<EmptyCommandResponse>
{
    /// <summary>
    /// 知识库id.
    /// </summary>
    public int WikiId { get; set; }

    /// <summary>
    /// 文档id.
    /// </summary>
    public int DocumentId { get; set; }

    /// <summary>
    /// 任务 id.
    /// </summary>
    public Guid TaskId { get; set; } = default!;
}