using MediatR;
using MoAI.Wiki.DocumentEmbedding.Models;

namespace MoAI.Wiki.DocumentEmbedding.Commands;

/// <summary>
/// 切割知识库文档.
/// </summary>
public class WikiDocumentTextPartitionPreviewCommand : IRequest<WikiDocumentTextPartitionPreviewCommandResponse>
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
    /// 每个分块的最大 token 数（必须大于 0）
    /// </summary>
    public int MaxTokensPerChunk { get; init; }

    /// <summary>
    /// 从一个块复制并重复到下一个块的标记数量，即重叠数量（必须大于等于 0）
    /// </summary>
    public int Overlap { get; init; }

    /// <summary>
    /// 可选，在每个分块前添加的标题.
    /// </summary>
    public string? ChunkHeader { get; set; }
}