using FluentValidation;
using MediatR;
using MoAI.AiModel.Models;
using MoAI.Infra.Models;

namespace MoAI.Wiki.Documents.Commands;

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
    /// 文本分割方法，暂时不支持使用.
    /// </summary>
    public string SplitMethod { get; set; } = default!;

    /// <summary>
    /// 每个段落最大 token 数量.
    /// </summary>
    public int MaxTokensPerParagraph { get; set; } = 1000;

    /// <summary>
    /// 块之间重叠令牌的数量.
    /// </summary>
    public int OverlappingTokens { get; set; } = 100;

    /// <summary>
    /// 统计 tokens 数量的算法 支持: "p50k", "cl100k", "o200k".
    /// </summary>
    public EmbeddingTokenizer Tokenizer { get; set; } = EmbeddingTokenizer.P50k;

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
