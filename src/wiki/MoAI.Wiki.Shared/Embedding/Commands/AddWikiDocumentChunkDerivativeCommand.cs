using FluentValidation;
using MediatR;
using MoAI.AI.Models;
using MoAI.Infra.Models;
using MoAI.Wiki.DocumentEmbedding.Models;
using MoAI.Wiki.Embedding.Models;

namespace MoAI.Wiki.Embedding.Commands;

/// <summary>
/// 批量增加元数据.
/// </summary>
public class AddWikiDocumentChunkDerivativeCommand : IRequest<EmptyCommandResponse>, IModelValidator<AddWikiDocumentChunkDerivativeCommand>
{
    /// <summary>
    /// 知识库id.
    /// </summary>
    public int WikiId { get; init; }

    /// <summary>
    /// 文档id.
    /// </summary>
    public int DocumentId { get; init; }

    /// <summary>
    /// 要处理的文本块的 chunkId 和内容.
    /// </summary>
    public IReadOnlyCollection<AddWikiDocumentDerivativeItem> Derivatives { get; init; } = Array.Empty<AddWikiDocumentDerivativeItem>();

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<AddWikiDocumentChunkDerivativeCommand> validate)
    {
        validate.RuleFor(x => x.WikiId).GreaterThan(0);
        validate.RuleFor(x => x.DocumentId).GreaterThan(0);
        validate.RuleFor(x => x.Derivatives).NotNull()
            .Must(x => x.Count > 0);
    }
}
