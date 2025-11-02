using FastEndpoints;
using FluentValidation;
using MoAI.Admin.User.Commands;

namespace MoAI.Admin.OAuth.Validators;

/// <summary>
/// DisableUserCommandValidator.
/// </summary>
public class DisableUserCommandValidator : Validator<DisableUserCommand>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DisableUserCommandValidator"/> class.
    /// </summary>
    public DisableUserCommandValidator()
    {
        RuleFor(x => x.UserIds)
            .NotEmpty().WithMessage("用户ID不能为空")
            .Must(x => x.Count > 0).WithMessage("用户ID不能为空")
            .Must(x => x.All(id => id > 0)).WithMessage("用户ID必须大于0");
    }
}
