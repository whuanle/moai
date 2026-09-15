using System.Text.Json.Serialization;
using MediatR;
using MoAI.App.Models;
using MoAI.App.Queries.Responses;
using MoAI.Infra.Models;

namespace MoAI.App.Queries;

/// <summary>
/// 查询外部 token 归属团队下全部已发布且未禁用的外部应用（团队级授权），需要外部 token.
/// </summary>
public class QueryExternalAuthorizedAppsCommand : IRequest<QueryExternalAuthorizedAppsCommandResponse>
{
    /// <summary>
    /// 外部 token 上下文，由 ExternalController 从外部 token claims 填充.
    /// </summary>
    [JsonIgnore]
    public ExternalTokenContext Context { get; init; } = default!;
}
