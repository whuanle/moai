using System.Text.Json.Serialization;
using FluentValidation;
using MediatR;
using MoAI.Infra.Models;
using MoAI.Infra.Services;
using MoAI.Skill.Queries.Responses;

namespace MoAI.Skill.Queries;

/// <summary>
/// 查询我的个人技能列表（不含系统内置与团队技能）.
/// </summary>
public class QueryMySkillListCommand : IRequest<QuerySkillListCommandResponse>, IModelValidator<QueryMySkillListCommand>, IUserIdContext
{
    /// <summary>
    /// 按名称/标识/描述关键字筛选，空为不过滤.
    /// </summary>
    public string? Keywords { get; init; }

    /// <summary>
    /// 按分类 id 过滤，为空查全部分类.
    /// </summary>
    public int? ClassifyId { get; init; }

    /// <inheritdoc/>
    [JsonIgnore]
    public long ContextUserId { get; init; }

    /// <inheritdoc/>
    [JsonIgnore]
    public UserType ContextUserType { get; init; }

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<QueryMySkillListCommand> validate)
    {
        validate.RuleFor(x => x.Keywords).MaximumLength(100).WithMessage("关键字最长 100 个字符.");
        validate.RuleFor(x => x.ClassifyId).GreaterThan(0).When(x => x.ClassifyId != null).WithMessage("分类 id 不正确.");
    }
}
