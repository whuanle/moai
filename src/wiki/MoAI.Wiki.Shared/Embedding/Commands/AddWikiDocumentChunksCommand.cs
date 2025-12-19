using FluentValidation;
using MediatR;
using MoAI.Infra.Models;
using MoAI.Wiki.DocumentEmbedding.Models;

namespace MoAI.Wiki.DocumentEmbedding.Commands;

/// <summary>
/// 手动添加一个新的块.
/// </summary>
public class AddWikiDocumentChunksCommand : IRequest<EmptyCommandResponse>, IModelValidator<AddWikiDocumentChunksCommand>
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
    /// 分块顺序.
    /// </summary>
    public int Order { get; set; }

    /// <summary>
    /// 分块文本.
    /// </summary>
    public string Text { get; set; } = string.Empty;

    /// <summary>
    /// 元数据.
    /// </summary>
    public IReadOnlyCollection<WikiDocumentDerivativeItem> Derivatives { get; init; } = Array.Empty<WikiDocumentDerivativeItem>();

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<AddWikiDocumentChunksCommand> validate)
    {
        validate.RuleFor(x => x.WikiId).GreaterThan(0).WithMessage("知识库ID错误");
        validate.RuleFor(x => x.DocumentId).GreaterThan(0).WithMessage("文档ID错误");
        validate.RuleFor(x => x.Text).NotEmpty().WithMessage("分块文本不能为空");
        validate.RuleFor(x => x.Derivatives).NotNull().WithMessage("分块文本不能为空");
    }
}
