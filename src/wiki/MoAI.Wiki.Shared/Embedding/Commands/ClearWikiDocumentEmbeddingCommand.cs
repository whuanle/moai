using FluentValidation;
using MediatR;
using MoAI.Infra.Models;

namespace MoAI.Wiki.Embedding.Commands;

/// <summary>
/// 清空知识库文档向量.
/// </summary>
public class ClearWikiDocumentEmbeddingCommand : IRequest<EmptyCommandResponse>, IModelValidator<ClearWikiDocumentEmbeddingCommand>
{
    /// <summary>
    /// 知识库 id.
    /// </summary>
    public int WikiId { get; init; }

    /// <summary>
    /// 不填写时清空整个知识库的文档向量.
    /// </summary>
    public int? DocumentId { get; init; }

    /// <summary>
    /// 是否删除索引.
    /// </summary>
    public bool IsAutoDeleteIndex { get; init; }

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<ClearWikiDocumentEmbeddingCommand> validate)
    {
        validate.RuleFor(x => x.WikiId)
            .NotEmpty().WithMessage("知识库id不正确")
            .GreaterThan(0).WithMessage("知识库id不正确");
    }
}
