using FluentValidation;
using MediatR;
using MoAI.Infra.Models;

namespace MoAI.Subsystem.Commands;

/// <summary>
/// 删除子系统（仅 root），软删除，appid 释放可复用.
/// </summary>
public class DeleteSubsystemCommand : IRequest<EmptyCommandResponse>, IModelValidator<DeleteSubsystemCommand>
{
    /// <summary>
    /// 子系统 id（由路由回填）.
    /// </summary>
    public Guid SubsystemId { get; set; }

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<DeleteSubsystemCommand> validate)
    {
    }
}
