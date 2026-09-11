using FluentValidation;
using MediatR;
using MoAI.AIPlugin.Queries.Responses;
using MoAI.Infra.Models;

namespace MoAI.TeamPlugin.Queries;

/// <summary>
/// 查询可用动态插件模板列表（注册表已发现的动态插件），仅团队成员可访问.
/// </summary>
public class QueryTeamDynamicTemplatesCommand : IUserIdContext, IRequest<QueryPluginListCommandResponse>, IModelValidator<QueryTeamDynamicTemplatesCommand>
{
    /// <inheritdoc/>
    public long ContextUserId { get; init; }

    /// <inheritdoc/>
    public UserType ContextUserType { get; init; }

    /// <summary>
    /// 团队 id.
    /// </summary>
    public long TeamId { get; init; }

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<QueryTeamDynamicTemplatesCommand> validate)
    {
        validate.RuleFor(x => x.TeamId).GreaterThan(0).WithMessage("团队 id 不正确.");
    }
}
