using System.Collections.Generic;

namespace MoAI.AIChannel.Queries.Responses;

/// <summary>
/// QueryAIChannelListCommandResponse.
/// </summary>
public class QueryAIChannelListCommandResponse
{
    /// <summary>
    /// 渠道列表.
    /// </summary>
    public IReadOnlyCollection<QueryAIChannelListCommandResponseItem> Items { get; init; } = new List<QueryAIChannelListCommandResponseItem>();
}
