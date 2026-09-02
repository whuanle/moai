using MediatR;
using MoAI.AIChannel.Queries.Responses;

namespace MoAI.AIChannel.Queries;

/// <summary>
/// 查询全部 AI 渠道列表.
/// </summary>
public class QueryAIChannelListCommand : IRequest<QueryAIChannelListCommandResponse>
{
}
