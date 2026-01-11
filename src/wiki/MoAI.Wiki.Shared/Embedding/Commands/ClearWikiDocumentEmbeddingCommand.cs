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
    /// 文档id列表.
    /// </summary>
    public IReadOnlyCollection<int> DocumentIds { get; init; } = Array.Empty<int>();

    /// <summary>
    /// 是否自动删除索引，如果知识库没有文档向量了，会自动删除向量数据索引.
    /// </summary>
    public bool IsAutoDeleteIndex { get; init; }

    /// <summary>
    /// 直接按照整个知识库清空.
    /// </summary>
    public bool? ClearAllDocuments { get; init; }

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<ClearWikiDocumentEmbeddingCommand> validate)
    {
        validate.RuleFor(x => x.WikiId)
            .NotEmpty().WithMessage("知识库id不正确")
            .GreaterThan(0).WithMessage("知识库id不正确");
    }
}
