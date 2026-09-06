using MediatR;
using MoAI.Gateway.Queries.Responses;

namespace MoAI.Gateway.Queries;

/// <summary>
/// 查询团队网关 API Key 列表，仅团队管理员可访问.
/// </summary>
public class QueryTeamApiKeysCommand : IRequest<QueryTeamApiKeysCommandResponse>
{
    /// <summary>
    /// 团队 id.
    /// </summary>
    public int TeamId { get; init; }
}
