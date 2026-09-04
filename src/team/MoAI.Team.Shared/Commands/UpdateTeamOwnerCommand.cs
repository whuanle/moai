using FluentValidation;
using MediatR;
using MoAI.Infra.Models;

namespace MoAI.Team.Commands;

/// <summary>
/// 转让团队所有权，仅 Owner 可操作；原 Owner 降为 Admin.
/// </summary>
public class UpdateTeamOwnerCommand : IRequest<EmptyCommandResponse>, IUserIdContext, IModelValidator<UpdateTeamOwnerCommand>
{
    /// <inheritdoc/>
    public long ContextUserId { get; init; }

    /// <inheritdoc/>
    public UserType ContextUserType { get; init; }

    /// <summary>
    /// 团队 id，由 Controller 从路由参数回填.
    /// </summary>
    public long TeamId { get; init; }

    /// <summary>
    /// 新所有者的用户 id，必须是团队成员.
    /// </summary>
    public long UserId { get; init; }

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<UpdateTeamOwnerCommand> validate)
    {
        // TeamId 由 Controller 从路由参数回填，自动验证发生在回填之前，因此此处只校验请求体字段.
        validate.RuleFor(x => x.UserId).GreaterThan(0).WithMessage("用户 id 不正确.");
    }
}
