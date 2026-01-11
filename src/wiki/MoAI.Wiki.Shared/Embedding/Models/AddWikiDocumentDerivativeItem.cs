using FluentValidation;
using MediatR;
using MoAI.AI.Models;

namespace MoAI.Wiki.DocumentEmbedding.Models;

/// <summary>
/// 切片元数据.
/// </summary>
public class AddWikiDocumentMetadataItem : IModelValidator<AddWikiDocumentMetadataItem>
{
    /// <summary>
    /// 关联的切片 id.
    /// </summary>
    public long ChunkId { get; init; }

    /// <summary>
    /// 元数据类型：1=大纲，2=问题，3=关键词，4=摘要，5=聚合的段.
    /// </summary>
    public ParagrahProcessorMetadataType MetadataType { get; init; }

    /// <summary>
    /// 提问/提纲/摘要内容.
    /// </summary>
    public string MetadataContent { get; init; } = default!;

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<AddWikiDocumentMetadataItem> validate)
    {
        validate.RuleFor(x => x.ChunkId).GreaterThan(0);
        validate.RuleFor(x => x.MetadataContent).NotEmpty();
    }
}