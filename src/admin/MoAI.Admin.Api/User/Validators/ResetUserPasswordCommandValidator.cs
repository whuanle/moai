using FastEndpoints;
using FluentValidation;
using MoAI.Admin.User.Commands;

namespace MoAI.Admin.OAuth.Validators;

/// <summary>
/// ResetUserPasswordCommandValidator.
/// </summary>
public class ResetUserPasswordCommandValidator : AbstractValidator<ResetUserPasswordCommand>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ResetUserPasswordCommandValidator"/> class.
    /// </summary>
    public ResetUserPasswordCommandValidator()
    {
        RuleFor(x => x.UserId)
            .GreaterThan(0).WithMessage("用户ID必须大于0")
            .WithMessage("用户ID不能为空");
    }
}