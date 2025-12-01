using MediatR;
using MoAI.Infra.Models;

namespace MoAI.Wiki.DocumentManager.Handlers;

/// <summary>
/// 删除知识库文档.
/// </summary>
public class DeleteWikiDocumentCommand : IRequest<EmptyCommandResponse>
{
    /// <summary>
    /// 知识库 id.
    /// </summary>
    public int WikiId { get; init; }

    /// <summary>
    /// 文档 id.
    /// </summary>
    public int DocumentId { get; init; }
}