using System.Text.Json.Serialization;
using MediatR;
using MoAI.App.Queries.Responses;
using MoAI.Infra.Models;

namespace MoAI.App.Queries;

/// <summary>
/// 查询平台公开应用（内部应用且已公开、已发布、未禁用），任意已登录用户可访问.
/// </summary>
public class QueryPublicAppsCommand : IRequest<QueryPublicAppsCommandResponse>, IUserIdContext
{
    /// <inheritdoc/>
    [JsonIgnore]
    public long ContextUserId { get; init; }

    /// <inheritdoc/>
    [JsonIgnore]
    public UserType ContextUserType { get; init; }
}
