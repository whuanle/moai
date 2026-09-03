using FluentValidation;
using MediatR;
using MoAI.Infra.Models;

namespace MoAI.Team.Commands;

/// <summary>
/// 移除团队成员，规则：Owner 可移除 Admin/Member；Admin 仅可移除 Member；成员可自行退出；Owner 不可被移除.
/// </summary>
public class RemoveTeamUserCommand : IRequest<EmptyCommandResponse>, IModelValidator<RemoveTeamUserCommand>
{
    /// <summary>
    /// 团队 id，由 Controller 从路由参数回填.
    /// </summary>
    public long TeamId { get; init; }

    /// <summary>
    /// 目标成员用户 id，由 Controller 从路由参数回填.
    /// </summary>
    public long UserId { get; init; }

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<RemoveTeamUserCommand> validate)
    {
        // TeamId/UserId 由 Controller 从路由参数回填，自动验证发生在回填之前，实体校验在 Handler 层完成.
    }
}
