using System;
using FluentValidation;
using MediatR;
using MoAI.Infra.Models;

namespace MoAI.TeamPlugin.Commands;

/// <summary>
/// 刷新团队 MCP 插件的工具列表，需团队 Owner/Admin.
/// </summary>
public class RefreshTeamMcpPluginCommand : IUserIdContext, IRequest<EmptyCommandResponse>, IModelValidator<RefreshTeamMcpPluginCommand>
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
    public static void Validate(AbstractValidator<RefreshTeamMcpPluginCommand> validate)
    {
        validate.RuleFor(x => x.TeamId).GreaterThan(0).WithMessage("团队 id 不正确.");
        validate.RuleFor(x => x.PluginId)
            .NotEmpty().WithMessage("插件 id 不正确.")
            .NotEqual(Guid.Empty).WithMessage("插件 id 不正确.");
    }
}
