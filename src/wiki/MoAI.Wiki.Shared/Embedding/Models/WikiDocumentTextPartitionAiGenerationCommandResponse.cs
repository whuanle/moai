using FluentValidation;
using MediatR;
using MoAI.AI.Models;
using MoAI.Infra.Models;

namespace MoAI.Wiki.Embedding.Models;

public class WikiDocumentTextPartitionAiGenerationCommandResponse
{
    /// <summary>
    /// 每段结果.
    /// </summary>
    public IReadOnlyCollection<KeyValue<long, ParagraphPreprocessResult>> Items { get; init; } = Array.Empty<KeyValue<long, ParagraphPreprocessResult>>();
}