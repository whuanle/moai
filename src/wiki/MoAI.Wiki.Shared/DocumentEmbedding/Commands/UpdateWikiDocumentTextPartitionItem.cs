using FluentValidation;
using MediatR;
using MoAI.Infra.Models;
using MoAI.Wiki.DocumentEmbedding.Models;
using MoAI.Wiki.Embeddings.Models;

namespace MoAI.Wiki.DocumentEmbedding.Commands;

public class UpdateWikiDocumentTextPartitionItem
{
    /// <summary>
    /// 分块顺序.
    /// </summary>
    public int Order { get; set; }

    /// <summary>
    /// 分块文本.
    /// </summary>
    public string Text { get; set; } = string.Empty;

    /// <inheritdoc/>
    public void Validate(AbstractValidator<WikiDocumentTextPartitionPreviewItem> validate)
    {
        validate.RuleFor(x => x.Text).NotEmpty().WithMessage("分块文本不能为空");
    }
}