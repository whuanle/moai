using System;
using FluentValidation;
using MediatR;
using MoAI.Infra.Models;

namespace MoAI.LoginSafety.Commands;

/// <summary>
/// 解封 IP（管理员手动），仅管理员，操作人由上下文自动注入.
/// </summary>
public class LiftIpBanCommand : IRequest<EmptyCommandResponse>, IModelValidator<LiftIpBanCommand>, IUserIdContext
{
    /// <summary>
    /// 封禁记录 id.
    /// </summary>
    public Guid Id { get; init; }

    /// <inheritdoc/>
    public long ContextUserId { get; init; }

    /// <inheritdoc/>
    public UserType ContextUserType { get; init; }

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<LiftIpBanCommand> validate)
    {
        validate.RuleFor(x => x.Id)
            .NotEqual(Guid.Empty).WithMessage("封禁记录 id 不能为空");
    }
}
