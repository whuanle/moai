using System.Text.Json.Serialization;
using MediatR;
using MoAI.App.Models;
using MoAI.App.Queries.Responses;

namespace MoAI.App.Queries;

/// <summary>
/// 查询外部会话消息（按 seq 升序），仅会话归属的外部用户且应用在其授权范围内可访问.
/// </summary>
public class QueryExternalSessionMessagesCommand : IRequest<QueryAppSessionMessagesCommandResponse>
{
    /// <summary>
    /// 会话 id（路由参数）.
    /// </summary>
    [JsonIgnore]
    public Guid SessionId { get; init; }

    /// <summary>
    /// 外部 token 上下文，由 ExternalController 从外部 token claims 填充.
    /// </summary>
    [JsonIgnore]
    public ExternalTokenContext Context { get; init; } = default!;
}
