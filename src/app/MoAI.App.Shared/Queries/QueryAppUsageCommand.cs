using System.Text.Json.Serialization;
using FluentValidation;
using MediatR;
using MoAI.Infra.Models;
using MoAI.Infra.Services;
using MoAI.App.Queries.Responses;

namespace MoAI.App.Queries;

/// <summary>
/// 查询应用用量统计（调用次数与 token，汇总 + 按模型分布；基于聚合用量表，近实时，最多滞后约 1 分钟）；需要团队 Admin 及以上角色.
/// </summary>
public class QueryAppUsageCommand : IRequest<QueryAppUsageCommandResponse>, IUserIdContext, IModelValidator<QueryAppUsageCommand>
{
    /// <summary>应用 id（来自路由）.</summary>
    public Guid AppId { get; init; }

    /// <inheritdoc/>
    [JsonIgnore]
    public long ContextUserId { get; init; }

    /// <inheritdoc/>
    [JsonIgnore]
    public UserType ContextUserType { get; init; }

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<QueryAppUsageCommand> validator)
    {
        // AppId 由路由 {id:guid} 保证非空，无需额外校验。
    }
}
