using FluentValidation;
using MediatR;
using MoAI.AI.Models;
using MoAI.Infra.Models;
using MoAI.Wiki.Embedding.Models;

namespace MoAI.Wiki.Embedding.Commands;

/// <summary>
/// 使用 Ai 对文本块进行策略处理.
/// </summary>
public class WikiDocumentTextPartitionAiGenerationCommand : IRequest<WikiDocumentTextPartitionAiGenerationCommandResponse>, IModelValidator<WikiDocumentTextPartitionAiGenerationCommand>
{
    /// <summary>
    /// 知识库id.
    /// </summary>
    public int WikiId { get; init; }

    /// <summary>
    /// Ai 模型.
    /// </summary>
    public int AiModelId { get; init; }

    /// <summary>
    /// 使用何种文档处理策略.
    /// </summary>
    public PreprocessStrategyType PreprocessStrategyType { get; init; }

    /// <summary>
    /// 要处理的文本块的 chunkId 和内容.
    /// </summary>
    public IReadOnlyCollection<KeyValue<long, string>> Chunks { get; init; } = Array.Empty<KeyValue<long, string>>();

    /// <inheritdoc/>
    public void Validate(AbstractValidator<WikiDocumentTextPartitionAiGenerationCommand> validate)
    {
        validate.RuleFor(x => x.WikiId).GreaterThan(0);
        validate.RuleFor(x => x.Chunks).NotNull()
            .Must(x => x.Count > 0);
    }
}
