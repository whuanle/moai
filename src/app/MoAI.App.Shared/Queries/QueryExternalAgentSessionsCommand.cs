using System.Text.Json.Serialization;
using MediatR;
using MoAI.App.Models;
using MoAI.App.Queries.Responses;

namespace MoAI.App.Queries;

/// <summary>
/// 查询当前外部用户在某应用下的会话列表（按最后消息时间倒序），需要外部用户 token.
/// </summary>
public class QueryExternalAgentSessionsCommand : IRequest<QueryExternalAgentSessionsCommandResponse>
{
    /// <summary>
    /// 目标应用 id（路由参数）.
    /// </summary>
    [JsonIgnore]
    public Guid AppId { get; init; }

    /// <summary>
    /// 外部 token 上下文，由 ExternalController 从外部 token claims 填充.
    /// </summary>
    [JsonIgnore]
    public ExternalTokenContext Context { get; init; } = default!;
}
