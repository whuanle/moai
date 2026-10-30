using FluentValidation;
using MediatR;
using MoAI.Infra.Models;
using MoAI.Subsystem.Queries.Responses;

namespace MoAI.Subsystem.Commands;

/// <summary>
/// 重置子系统接入密钥（仅 root），旧密钥立即失效，返回新密钥原文（仅此一次）.
/// </summary>
public class ResetSubsystemSecretCommand : IRequest<ResetSubsystemSecretCommandResponse>, IModelValidator<ResetSubsystemSecretCommand>
{
    /// <summary>
    /// 子系统 id（由路由回填）.
    /// </summary>
    public Guid SubsystemId { get; set; }

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<ResetSubsystemSecretCommand> validate)
    {
    }
}
