using FluentValidation;
using MediatR;
using MoAI.Infra.Models;

namespace MoAI.Team.Commands;

/// <summary>
/// 管理员转让团队负责人，仅管理员可操作；目标用户可为系统内任意用户，非团队成员时自动加入团队成为负责人，原负责人降为管理员.
/// </summary>
public class AdminTransferTeamOwnerCommand : IRequest<EmptyCommandResponse>, IModelValidator<AdminTransferTeamOwnerCommand>
{
    /// <summary>
    /// 团队 id，由 Controller 从路由参数回填.
    /// </summary>
    public long TeamId { get; init; }

    /// <summary>
    /// 接手负责人的目标用户 id.
    /// </summary>
    public long UserId { get; init; }

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<AdminTransferTeamOwnerCommand> validate)
    {
        // TeamId 由 Controller 从路由参数回填，自动验证发生在回填之前，因此此处只校验请求体字段.
        validate.RuleFor(x => x.UserId).GreaterThan(0).WithMessage("目标用户不能为空.");
    }
}
