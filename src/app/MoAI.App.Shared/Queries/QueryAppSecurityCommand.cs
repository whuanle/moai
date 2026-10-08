using System.Text.Json.Serialization;
using MediatR;
using MoAI.App.Queries.Responses;
using MoAI.Infra.Models;
using MoAI.Infra.Services;

namespace MoAI.App.Queries;

/// <summary>
/// 查询应用安全配置（内容脱敏规则），仅团队成员可访问；未保存过配置时返回默认值.
/// </summary>
public class QueryAppSecurityCommand : IRequest<QueryAppSecurityCommandResponse>, IUserIdContext
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
