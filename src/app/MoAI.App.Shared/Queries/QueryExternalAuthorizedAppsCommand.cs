using System.Text.Json.Serialization;
using MediatR;
using MoAI.App.Models;
using MoAI.App.Queries.Responses;
using MoAI.Infra.Models;

namespace MoAI.App.Queries;

/// <summary>
/// 查询当前外部 token 授权范围内的应用列表（已发布且未禁用），需要外部 token.
/// </summary>
public class QueryExternalAuthorizedAppsCommand : IRequest<QueryExternalAuthorizedAppsCommandResponse>
{
    /// <summary>
    /// 外部 token 上下文，由 ExternalController 从外部 token claims 填充.
    /// </summary>
    [JsonIgnore]
    public ExternalTokenContext Context { get; init; } = default!;
}
