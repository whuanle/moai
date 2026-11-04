using FluentValidation;
using MediatR;
using MoAI.Infra.Models;
using MoAI.Infra.Services;
using MoAI.Memory.Queries.Responses;
using System.Text.Json.Serialization;

namespace MoAI.Memory.Queries;

/// <summary>
/// 分页列举本人的全局记忆（按创建时间倒序）.
/// </summary>
public class QueryMyGlobalMemoriesCommand : IRequest<QueryMyGlobalMemoriesCommandResponse>, IModelValidator<QueryMyGlobalMemoriesCommand>, IUserIdContext
{
    /// <summary>
    /// 页码（1 起）.
    /// </summary>
    public int Page { get; init; } = 1;

    /// <summary>
    /// 页大小（1~100）.
    /// </summary>
    public int PageSize { get; init; } = 20;

    /// <summary>
    /// 关键字（按记忆内容模糊匹配，区分中英文，不区分大小写；留空返回全部）.
    /// </summary>
    public string? Keywords { get; init; }

    /// <inheritdoc/>
    [JsonIgnore]
    public long ContextUserId { get; init; }

    /// <inheritdoc/>
    [JsonIgnore]
    public UserType ContextUserType { get; init; }

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<QueryMyGlobalMemoriesCommand> validate)
    {
        validate.RuleFor(x => x.Page).GreaterThanOrEqualTo(1).WithMessage("页码不正确.");
        validate.RuleFor(x => x.PageSize).InclusiveBetween(1, 100).WithMessage("页大小不正确.");
        validate.RuleFor(x => x.Keywords).MaximumLength(100).WithMessage("关键字不能超过 100 字.");
    }
}
