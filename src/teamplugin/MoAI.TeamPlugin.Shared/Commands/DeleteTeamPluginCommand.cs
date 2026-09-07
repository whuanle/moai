using System;
using FluentValidation;
using MediatR;
using MoAI.Infra.Models;

namespace MoAI.TeamPlugin.Commands;

/// <summary>
/// 删除团队插件（仅团队自有插件），需团队 Owner/Admin.
/// </summary>
public class DeleteTeamPluginCommand : IUserIdContext, IRequest<EmptyCommandResponse>, IModelValidator<DeleteTeamPluginCommand>
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
    public static void Validate(AbstractValidator<DeleteTeamPluginCommand> validate)
    {
        validate.RuleFor(x => x.TeamId).GreaterThan(0).WithMessage("团队 id 不正确.");
        validate.RuleFor(x => x.PluginId)
            .NotEmpty().WithMessage("插件id不正确.")
            .NotEqual(Guid.Empty).WithMessage("插件id不正确.");
    }
}
