using MediatR;
using MoAI.Gateway.Queries.Responses;

namespace MoAI.Gateway.Queries;

/// <summary>
/// 查询团队可用的网关模型（已授权给该团队 + 额度状态），团队成员可访问.
/// </summary>
public class QueryTeamGatewayModelsCommand : IRequest<QueryTeamGatewayModelsCommandResponse>
{
    /// <summary>
    /// 团队 id.
    /// </summary>
    public int TeamId { get; init; }
}
