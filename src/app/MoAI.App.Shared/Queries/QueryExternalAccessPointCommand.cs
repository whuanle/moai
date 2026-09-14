using System.Text.Json.Serialization;
using MediatR;
using MoAI.App.Queries.Responses;

namespace MoAI.App.Queries;

/// <summary>
/// 查询外部应用访问点公开配置（悬浮组件用，匿名可访问）；应用不存在/非外部应用返回 404.
/// </summary>
public class QueryExternalAccessPointCommand : IRequest<ExternalAccessPointResponse>
{
    /// <summary>
    /// 应用 id.
    /// </summary>
    [JsonIgnore]
    public Guid AppId { get; init; }
}
