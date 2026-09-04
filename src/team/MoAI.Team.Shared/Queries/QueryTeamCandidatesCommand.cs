using FluentValidation;
using MediatR;
using MoAI.Team.Queries.Responses;

namespace MoAI.Team.Queries;

/// <summary>
/// 查询可邀请的候选用户（模糊匹配用户名/昵称/邮箱），供团队 Owner/Admin 邀请成员时搜索使用；已入团成员会被排除.
/// </summary>
public class QueryTeamCandidatesCommand : IRequest<QueryTeamCandidatesCommandResponse>, IModelValidator<QueryTeamCandidatesCommand>
{
    /// <summary>
    /// 团队 id，由 Controller 从路由参数回填.
    /// </summary>
    public long TeamId { get; init; }

    /// <summary>
    /// 搜索关键字，模糊匹配用户名/昵称/邮箱.
    /// </summary>
    public string? Keyword { get; set; }

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<QueryTeamCandidatesCommand> validate)
    {
        // TeamId 由 Controller 从路由参数回填，自动验证发生在回填之前，因此此处只校验请求体字段.
        validate.RuleFor(x => x.Keyword).MaximumLength(50).WithMessage("搜索关键字最长 50 个字符.");
    }
}
