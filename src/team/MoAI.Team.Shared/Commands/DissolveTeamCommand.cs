using FluentValidation;
using MediatR;
using MoAI.Infra.Models;

namespace MoAI.Team.Commands;

/// <summary>
/// 解散团队，仅 Owner 可操作；团队与全部成员关系软删除.
/// </summary>
public class DissolveTeamCommand : IRequest<EmptyCommandResponse>, IModelValidator<DissolveTeamCommand>
{
    /// <summary>
    /// 团队 id，由 Controller 从路由参数回填.
    /// </summary>
    public long TeamId { get; init; }

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<DissolveTeamCommand> validate)
    {
        // TeamId 由 Controller 从路由参数回填，自动验证发生在回填之前，实体校验在 Handler 层完成.
    }
}
