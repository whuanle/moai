using System.Text.Json.Serialization;
using MediatR;
using MoAI.App.Queries.Responses;
using MoAI.Infra.Models;

namespace MoAI.App.Queries;

/// <summary>
/// 查询外部应用访问点配置（内部管理视图），需要团队 Admin 及以上角色.
/// </summary>
public class QueryAppAccessPointCommand : IRequest<AppAccessPointConfigResponse>, IUserIdContext
{
    /// <summary>
    /// 应用 id.
    /// </summary>
    [JsonIgnore]
    public Guid AppId { get; init; }

    /// <inheritdoc/>
    [JsonIgnore]
    public long ContextUserId { get; init; }

    /// <inheritdoc/>
    [JsonIgnore]
    public UserType ContextUserType { get; init; }
}
