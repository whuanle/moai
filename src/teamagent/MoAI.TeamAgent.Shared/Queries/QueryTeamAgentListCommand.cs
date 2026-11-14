using System.Text.Json.Serialization;
using FluentValidation;
using MediatR;
using MoAI.Infra.Models;
using MoAI.TeamAgent.Queries.Responses;

namespace MoAI.TeamAgent.Queries;

/// <summary>
/// 查询团队智能体列表，仅团队成员可访问.
/// </summary>
public class QueryTeamAgentListCommand : IRequest<QueryTeamAgentListCommandResponse>, IModelValidator<QueryTeamAgentListCommand>, IUserIdContext
{
    /// <summary>
    /// 团队 id.
    /// </summary>
    public long TeamId { get; init; }

    /// <summary>
    /// 按名称/描述关键字筛选，空为不过滤.
    /// </summary>
    public string? SearchText { get; init; }

    /// <inheritdoc/>
    [JsonIgnore]
    public long ContextUserId { get; init; }

    /// <inheritdoc/>
    [JsonIgnore]
    public UserType ContextUserType { get; init; }

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<QueryTeamAgentListCommand> validate)
    {
        validate.RuleFor(x => x.TeamId).GreaterThan(0).WithMessage("团队 id 不正确.");
        validate.RuleFor(x => x.SearchText).MaximumLength(100).WithMessage("关键字最长 100 个字符.");
    }
}
