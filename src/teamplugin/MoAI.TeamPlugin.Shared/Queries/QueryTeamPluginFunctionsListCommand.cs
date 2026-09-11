using System;
using FluentValidation;
using MediatR;
using MoAI.AIPlugin.Queries.Responses;
using MoAI.Infra.Models;

namespace MoAI.TeamPlugin.Queries;

/// <summary>
/// 获取团队插件的函数列表，仅团队成员可访问.
/// </summary>
public class QueryTeamPluginFunctionsListCommand : IUserIdContext, IRequest<QueryCustomPluginFunctionsListCommandResponse>, IModelValidator<QueryTeamPluginFunctionsListCommand>
{
    /// <inheritdoc/>
    public long ContextUserId { get; init; }

    /// <inheritdoc/>
    public UserType ContextUserType { get; init; }

    /// <summary>
    /// 团队 id.
    /// </summary>
    public long TeamId { get; init; }

    /// <summary>
    /// 插件记录 id.
    /// </summary>
    public Guid PluginId { get; init; }

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<QueryTeamPluginFunctionsListCommand> validate)
    {
        validate.RuleFor(x => x.TeamId).GreaterThan(0).WithMessage("团队 id 不正确.");
        validate.RuleFor(x => x.PluginId)
            .NotEmpty().WithMessage("插件 id 错误.")
            .NotEqual(Guid.Empty).WithMessage("插件 id 错误.");
    }
}
