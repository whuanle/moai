using System.Text.Json.Serialization;
using FluentValidation;
using MediatR;
using MoAI.App.Queries.Responses;
using MoAI.Infra.Models;
using MoAI.Infra.Services;

namespace MoAI.App.Queries;

/// <summary>
/// 查询应用使用趋势（近 N 天使用次数与消息条数按日聚合，数据来自统计模块统一日表，最多滞后约 1 分钟）；需要团队 Admin 及以上角色.
/// </summary>
public class QueryAppUsageTrendCommand : IRequest<QueryAppUsageTrendCommandResponse>, IUserIdContext, IModelValidator<QueryAppUsageTrendCommand>
{
    /// <summary>应用 id（来自路由）.</summary>
    public Guid AppId { get; init; }

    /// <summary>趋势区间天数（默认 30，上限 90）.</summary>
    public int Days { get; init; } = 30;

    /// <inheritdoc/>
    [JsonIgnore]
    public long ContextUserId { get; init; }

    /// <inheritdoc/>
    [JsonIgnore]
    public UserType ContextUserType { get; init; }

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<QueryAppUsageTrendCommand> validator)
    {
        validator.RuleFor(x => x.Days).InclusiveBetween(1, 90).WithMessage("趋势天数需要在 1 到 90 之间.");
    }
}
