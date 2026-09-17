using System.Text.Json.Serialization;
using FluentValidation;
using MediatR;
using MoAI.Infra.Models;
using MoAI.Infra.Services;
using MoAI.Skill.Queries.Responses;

namespace MoAI.Skill.Queries;

/// <summary>
/// 查询团队技能列表，仅团队成员可访问.
/// </summary>
public class QueryTeamSkillListCommand : IRequest<QuerySkillListCommandResponse>, IModelValidator<QueryTeamSkillListCommand>, IUserIdContext
{
    /// <summary>
    /// 团队 id.
    /// </summary>
    public int TeamId { get; init; }

    /// <summary>
    /// 按名称/标识/描述关键字筛选，空为不过滤.
    /// </summary>
    public string? Keywords { get; init; }

    /// <inheritdoc/>
    [JsonIgnore]
    public long ContextUserId { get; init; }

    /// <inheritdoc/>
    [JsonIgnore]
    public UserType ContextUserType { get; init; }

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<QueryTeamSkillListCommand> validate)
    {
        validate.RuleFor(x => x.TeamId).GreaterThan(0).WithMessage("团队 id 不正确.");
        validate.RuleFor(x => x.Keywords).MaximumLength(100).WithMessage("关键字最长 100 个字符.");
    }
}
