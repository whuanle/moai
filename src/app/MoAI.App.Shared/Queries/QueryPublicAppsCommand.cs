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
    /// <summary>
    /// 名称/描述关键字，空则不过滤.
    /// </summary>
    public string? Keywords { get; init; }

    /// <summary>
    /// 分类 id，大于 0 时按分类过滤.
    /// </summary>
    public int? ClassifyId { get; init; }

    /// <inheritdoc/>
    [JsonIgnore]
    public long ContextUserId { get; init; }

    /// <inheritdoc/>
    [JsonIgnore]
    public UserType ContextUserType { get; init; }
}
