using FluentValidation;
using MediatR;
using MoAI.AiModel.Models;
using MoAI.Infra.Models;

namespace MoAI.Wiki.Embedding.Commands;

/// <summary>
/// 向量化文档.
/// </summary>
public class EmbeddingDocumentCommand : IRequest<EmptyCommandResponse>, IModelValidator<EmbeddingDocumentCommand>
{
    /// <summary>
    /// 知识库 id.
    /// </summary>
    public int WikiId { get; init; }

    /// <summary>
    /// 文档 id.
    /// </summary>
    public int DocumentId { get; init; }

    /// <summary>
    /// 是否将 chunk 源文本也向量化.
    /// </summary>
    public bool IsEmbedSourceText { get; init; } = true;

    /// <summary>
    /// 向量化衍生数据.
    /// </summary>
    public bool IsEmbedDerivedData { get; init; } = true;

    /// <inheritdoc/>
    public void Validate(AbstractValidator<EmbeddingDocumentCommand> validate)
    {
        validate.RuleFor(x => x.WikiId)
            .NotEmpty().WithMessage("知识库id不正确")
            .GreaterThan(0).WithMessage("知识库id不正确");

        validate.RuleFor(x => x.DocumentId)
            .NotEmpty().WithMessage("文档id不正确")
            .GreaterThan(0).WithMessage("文档id不正确");
    }
}
