using System;
using MediatR;
using MoAI.AIChannel.Queries.Responses;

namespace MoAI.AIChannel.Queries;

/// <summary>
/// 查询渠道级授权视图：渠道下模型数量统计 + 各私有模型授权团队的并集（用作批量授权弹窗的回显）.
/// </summary>
public class QueryAIChannelAuthorizationCommand : IRequest<QueryAIChannelAuthorizationCommandResponse>
{
    /// <summary>
    /// 渠道 id.
    /// </summary>
    public Guid ChannelId { get; init; }
}
