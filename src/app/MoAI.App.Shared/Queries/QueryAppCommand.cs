using System.Text.Json.Serialization;
using MediatR;
using MoAI.App.Queries.Responses;
using MoAI.Infra.Models;
using MoAI.Infra.Services;

namespace MoAI.App.Queries;

/// <summary>
/// 查询应用详情，仅团队成员可访问.
/// </summary>
public class QueryAppCommand : IRequest<QueryAppCommandResponse>, IUserIdContext
{
    /// <summary>
    /// 应用 id，由 Controller 从路由参数回填.
    /// </summary>
    public Guid AppId { get; init; }

    /// <inheritdoc/>
    [JsonIgnore]
    public long ContextUserId { get; init; }

    /// <inheritdoc/>
    [JsonIgnore]
    public UserType ContextUserType { get; init; }
}
