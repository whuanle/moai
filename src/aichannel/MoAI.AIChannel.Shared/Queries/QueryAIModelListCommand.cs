using System;
using MediatR;
using MoAI.AIChannel.Queries.Responses;

namespace MoAI.AIChannel.Queries;

/// <summary>
/// 查询 AI 模型列表.
/// </summary>
public class QueryAIModelListCommand : IRequest<QueryAIModelListCommandResponse>
{
    /// <summary>
    /// 团队 id；传入时仅返回公开或已授权给该团队的模型.
    /// </summary>
    public int? TeamId { get; init; }

    /// <summary>
    /// 渠道 id，为空时查询全部.
    /// </summary>
    public Guid? ChannelId { get; init; }

    /// <summary>
    /// 团队自有渠道归属团队 id，大于 0 时仅返回该团队自有渠道下的模型（团队模型渠道页使用）.
    /// </summary>
    public int OwnerTeamId { get; init; }
}
