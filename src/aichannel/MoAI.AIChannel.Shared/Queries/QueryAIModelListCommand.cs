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
    /// 渠道 id，为空时查询全部.
    /// </summary>
    public Guid? ChannelId { get; init; }
}
