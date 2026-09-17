using System.Text.Json.Serialization;
using FluentValidation;
using MediatR;
using MoAI.Infra.Models;
using MoAI.Infra.Services;
using MoAI.Skill.Queries.Responses;

namespace MoAI.Skill.Queries;

/// <summary>
/// 查询技能详情；可见即可查看：系统内置/已公开/所在团队/本人个人技能，平台管理员放行.
/// </summary>
public class QuerySkillCommand : IRequest<QuerySkillCommandResponse>, IModelValidator<QuerySkillCommand>, IUserIdContext
{
    /// <summary>
    /// 技能 id.
    /// </summary>
    public Guid SkillId { get; init; }

    /// <inheritdoc/>
    [JsonIgnore]
    public long ContextUserId { get; init; }

    /// <inheritdoc/>
    [JsonIgnore]
    public UserType ContextUserType { get; init; }

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<QuerySkillCommand> validate)
    {
        validate.RuleFor(x => x.SkillId).NotEmpty().WithMessage("技能 id 不正确.");
    }
}
