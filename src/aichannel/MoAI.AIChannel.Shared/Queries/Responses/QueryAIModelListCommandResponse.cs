using System.Collections.Generic;

namespace MoAI.AIChannel.Queries.Responses;

/// <summary>
/// QueryAIModelListCommandResponse.
/// </summary>
public class QueryAIModelListCommandResponse
{
    /// <summary>
    /// 模型列表.
    /// </summary>
    public IReadOnlyCollection<QueryAIModelListCommandResponseItem> Items { get; init; } = new List<QueryAIModelListCommandResponseItem>();
}
