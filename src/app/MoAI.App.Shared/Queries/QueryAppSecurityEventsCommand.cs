using System.Text.Json.Serialization;
using MediatR;
using MoAI.App.Queries.Responses;
using MoAI.Infra.Models;
using MoAI.Infra.Services;

namespace MoAI.App.Queries;

/// <summary>
/// 分页查询应用安全事件（规则命中审计，按时间倒序）；需要团队 Admin 及以上角色.
/// </summary>
public class QueryAppSecurityEventsCommand : PagedParamter, IRequest<QueryAppSecurityEventsCommandResponse>, IUserIdContext
{
    /// <summary>应用 id（来自路由）.</summary>
    public Guid AppId { get; init; }

    /// <summary>按作用域过滤（input/toolResult/toolArgs/modelOutput）；为空不过滤.</summary>
    public string? Scope { get; init; }

    /// <inheritdoc/>
    [JsonIgnore]
    public long ContextUserId { get; init; }

    /// <inheritdoc/>
    [JsonIgnore]
    public UserType ContextUserType { get; init; }
}
