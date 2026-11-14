using FluentValidation;
using MediatR;
using MoAI.Infra.Models;
using MoAI.Subsystem.Commands.Responses;

namespace MoAI.Subsystem.Commands;

/// <summary>
/// 重置系统级接入密钥（仅 root），旧密钥立即失效，返回新密钥原文（仅此一次）.
/// </summary>
public class ResetSubsysAppSecretCommand : IRequest<ResetSubsysAppSecretCommandResponse>, IModelValidator<ResetSubsysAppSecretCommand>
{
    /// <summary>
    /// 接入 id（由路由回填）.
    /// </summary>
    public Guid Id { get; set; }

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<ResetSubsysAppSecretCommand> validate)
    {
    }
}
