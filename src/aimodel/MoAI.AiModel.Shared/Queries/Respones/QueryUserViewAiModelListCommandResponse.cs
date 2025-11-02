using MediatR;
using MoAI.AiModel.Models;

namespace MoAI.AiModel.Queries.Respones;

/// <summary>
/// <inheritdoc cref="QueryUserViewAiModelListCommand"/>
/// </summary>
public class QueryUserViewAiModelListCommandResponse
{
    /// <summary>
    /// 模型列表.
    /// </summary>
    public IReadOnlyCollection<PublicModelInfo> AiModels { get; init; } = Array.Empty<PublicModelInfo>();
}
