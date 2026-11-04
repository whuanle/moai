using System.Text.Json.Serialization;
using FluentValidation;
using MediatR;
using MoAI.App.Queries.Responses;
using MoAI.Infra.Models;
using MoAI.Infra.Services;

namespace MoAI.App.Queries;

/// <summary>
/// 查询应用对外功能开关与团队资源 key（应用 ACP/A2A 共用一把；未开启时 key 为空），需要团队 Admin 及以上角色.
/// </summary>
public class QueryAppFeatureCommand : IRequest<AppFeatureResponse>, IUserIdContext, IModelValidator<QueryAppFeatureCommand>
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

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<QueryAppFeatureCommand> validate)
    {
        // AppId 由 Controller 从路由参数回填，不做校验.
    }
}
