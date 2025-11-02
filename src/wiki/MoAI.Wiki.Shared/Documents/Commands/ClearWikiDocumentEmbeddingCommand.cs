using MediatR;
using MoAI.Infra.Models;

namespace MoAI.Wiki.Documents.Commands;

/// <summary>
/// 清空知识库文档向量.
/// </summary>
public class ClearWikiDocumentEmbeddingCommand : IRequest<EmptyCommandResponse>
{
    /// <summary>
    /// 知识库 id.
    /// </summary>
    public int WikiId { get; init; }

    /// <summary>
    /// 不填写时清空整个知识库的文档向量.
    /// </summary>
    public int? DocumentId { get; init; }
}
