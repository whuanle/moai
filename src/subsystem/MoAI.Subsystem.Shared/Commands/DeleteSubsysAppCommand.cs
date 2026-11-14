using FluentValidation;
using MediatR;
using MoAI.Infra.Models;

namespace MoAI.Subsystem.Commands;

/// <summary>
/// 删除系统级接入（仅 root，软删除），appid 立即失效；子系统行同步清除团队授权与在线在场.
/// </summary>
public class DeleteSubsysAppCommand : IRequest<EmptyCommandResponse>, IModelValidator<DeleteSubsysAppCommand>
{
    /// <summary>
    /// 接入 id（由路由回填）.
    /// </summary>
    public Guid Id { get; set; }

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<DeleteSubsysAppCommand> validate)
    {
    }
}
