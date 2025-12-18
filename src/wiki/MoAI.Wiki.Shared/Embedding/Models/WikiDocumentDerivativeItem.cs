using FluentValidation;
using MediatR;
using MoAI.AI.Models;

namespace MoAI.Wiki.DocumentEmbedding.Models;

/// <summary>
/// 切片元数据.
/// </summary>
public class WikiDocumentDerivativeItem : IModelValidator<WikiDocumentDerivativeItem>
{
    /// <summary>
    /// 元数据类型：1=大纲，2=问题，3=关键词，4=摘要，5=聚合的段.
    /// </summary>
    public ParagrahProcessorMetadataType DerivativeType { get; init; }

    /// <summary>
    /// 提问/提纲/摘要内容.
    /// </summary>
    public string DerivativeContent { get; init; } = default!;

    /// <inheritdoc/>
    public void Validate(AbstractValidator<WikiDocumentDerivativeItem> validate)
    {
        validate.RuleFor(x => x.DerivativeContent).NotEmpty();
    }
}
