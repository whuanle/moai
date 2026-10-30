using MediatR;
using MoAI.Subsystem.Queries.Responses;

namespace MoAI.Subsystem.Queries;

/// <summary>
/// 子系统自省（机器面，子系统 token 鉴权）：返回自身身份与团队授权概况.
/// </summary>
public class QuerySubsystemInfoCommand : IRequest<QuerySubsystemInfoCommandResponse>
{
    /// <summary>
    /// 子系统 id（由服务端从认证上下文回填，不接受请求参数注入）.
    /// </summary>
    public Guid SubsystemId { get; init; }
}
