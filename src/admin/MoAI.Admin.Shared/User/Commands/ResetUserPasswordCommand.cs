using FluentValidation;
using MediatR;
using MoAI.Infra.Defaults;
using MoAI.Infra.Models;

namespace MoAI.Admin.User.Commands;

/// <summary>
/// 重置用户密码.
/// </summary>
public class ResetUserPasswordCommand : IRequest<SimpleString>, IModelValidator<ResetUserPasswordCommand>
{
    /// <summary>
    /// 用户 id.
    /// </summary>
    public int UserId { get; init; }

    /// <inheritdoc/>
    public void Validate(AbstractValidator<ResetUserPasswordCommand> validate)
    {
        validate.RuleFor(x => x.UserId)
            .GreaterThan(0).WithMessage("用户ID必须大于0")
            .WithMessage("用户ID不能为空");
    }
}
