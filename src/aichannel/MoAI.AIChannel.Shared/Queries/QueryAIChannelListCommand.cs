using MediatR;
using MoAI.AIChannel.Queries.Responses;

namespace MoAI.AIChannel.Queries;

/// <summary>
/// 查询全部 AI 渠道列表.
/// </summary>
public class QueryAIChannelListCommand : IRequest<QueryAIChannelListCommandResponse>
{
    /// <summary>
    /// 渠道归属团队 id，0=系统渠道（管理端使用）；团队模型渠道页传入自己的团队 id.
    /// </summary>
    public int TeamId { get; init; }
}
