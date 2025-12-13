using FluentValidation;
using MediatR;
using MoAI.AI.Models;

namespace MoAI.Wiki.DocumentEmbedding.Models;

/// <summary>
/// 切片衍生内容.
/// </summary>
public class AddWikiDocumentDerivativeItem : IModelValidator<AddWikiDocumentDerivativeItem>
{
    /// <summary>
    /// 关联的切片 id.
    /// </summary>
    public long ChunkId { get; init; }

    /// <summary>
    /// 衍生类型：1=大纲，2=问题，3=关键词，4=摘要，5=聚合的段.
    /// </summary>
    public ParagrahProcessorMetadataType DerivativeType { get; init; }

    /// <summary>
    /// 提问/提纲/摘要内容.
    /// </summary>
    public string DerivativeContent { get; init; } = default!;

    /// <inheritdoc/>
    public void Validate(AbstractValidator<AddWikiDocumentDerivativeItem> validate)
    {
        validate.RuleFor(x => x.ChunkId).GreaterThan(0);
        validate.RuleFor(x => x.DerivativeContent).NotEmpty();
    }
}