using FluentValidation;
using MediatR;

namespace MoAI.Wiki.DocumentEmbedding.Models;

/// <summary>
/// 文档内容.
/// </summary>
public class WikiDocumenChunkItem : IModelValidator<WikiDocumenChunkItem>
{
    /// <summary>
    /// 切片 id.
    /// </summary>
    public long ChunkId { get; init; }

    /// <summary>
    /// 分块顺序.
    /// </summary>
    public int Order { get; set; }

    /// <summary>
    /// 分块文本.
    /// </summary>
    public string Text { get; set; } = string.Empty;

    /// <summary>
    /// 衍生内容.
    /// </summary>
    public IReadOnlyCollection<WikiDocumentDerivativeItem> Derivatives { get; init; } = Array.Empty<WikiDocumentDerivativeItem>();

    /// <inheritdoc/>
    public void Validate(AbstractValidator<WikiDocumenChunkItem> validate)
    {
        validate.RuleFor(x => x.ChunkId).GreaterThan(0).WithMessage("分块ID错误");
        validate.RuleFor(x => x.Text).NotEmpty().WithMessage("分块文本不能为空");
    }
}