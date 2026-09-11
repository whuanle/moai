using FluentValidation;
using MediatR;
using MoAI.Infra.Models;

namespace MoAI.Team.Commands;

/// <summary>
/// 禁用/启用团队，仅管理员可操作；禁用后团队及其下级资源停用，不影响成员账号登录.
/// </summary>
public class UpdateTeamDisableCommand : IRequest<EmptyCommandResponse>, IModelValidator<UpdateTeamDisableCommand>
{
    /// <summary>
    /// 团队 id，由 Controller 从路由参数回填.
    /// </summary>
    public long TeamId { get; init; }

    /// <summary>
    /// 是否禁用：true=禁用 false=启用.
    /// </summary>
    public bool IsDisable { get; init; }

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<UpdateTeamDisableCommand> validate)
    {
        // TeamId 由 Controller 从路由参数回填，自动验证发生在回填之前，实体校验在 Handler 层完成.
    }
}
