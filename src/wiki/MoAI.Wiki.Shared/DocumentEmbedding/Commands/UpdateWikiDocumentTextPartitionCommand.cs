using MediatR;
using MoAI.Infra.Models;

namespace MoAI.Wiki.DocumentEmbedding.Commands;

/// <summary>
/// 更新切片列表.
/// </summary>
public class UpdateWikiDocumentTextPartitionCommand : IRequest<EmptyCommandResponse>
{
    /// <summary>
    /// 知识库id.
    /// </summary>
    public int WikiId { get; init; }

    /// <summary>
    /// 知识库 id.
    /// </summary>
    public int DocumentId { get; init; }

    /// <summary>
    /// 切片列表.
    /// </summary>
    public IReadOnlyCollection<UpdateWikiDocumentTextPartitionItem> Items { get; init; } = Array.Empty<UpdateWikiDocumentTextPartitionItem>();
}
