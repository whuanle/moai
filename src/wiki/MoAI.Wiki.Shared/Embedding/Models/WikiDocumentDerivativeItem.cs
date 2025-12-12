using FluentValidation;
using MediatR;
using MoAI.AI.Models;

namespace MoAI.Wiki.DocumentEmbedding.Models;

/// <summary>
/// 切片衍生内容.
/// </summary>
public class WikiDocumentDerivativeItem : AbstractValidator<WikiDocumentDerivativeItem>
{
    /// <summary>
    /// 衍生类型：1=大纲，2=问题，3=关键词，4=摘要，5=聚合的段.
    /// </summary>
    public ParagrahProcessorMetadataType DerivativeType { get; set; }

    /// <summary>
    /// 提问/提纲/摘要内容.
    /// </summary>
    public string DerivativeContent { get; set; } = default!;
}