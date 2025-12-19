using FluentValidation;
using MediatR;
using MoAI.Infra.Defaults;
using MoAI.Infra.Models;

namespace MoAI.Admin.User.Commands;

/// <summary>
/// 禁用启用用户.
/// </summary>
public class DisableUserCommand : IRequest<EmptyCommandResponse>, IModelValidator<DisableUserCommand>
{
    /// <summary>
    /// 用户ID.
    /// </summary>
    public IReadOnlyCollection<int> UserIds { get; init; } = Array.Empty<int>();

    /// <summary>
    /// 是否禁用.
    /// </summary>
    public bool IsDisable { get; init; }

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<DisableUserCommand> validate)
    {
        validate.RuleFor(x => x.UserIds)
            .NotEmpty().WithMessage("用户ID不能为空")
            .Must(x => x.Count > 0).WithMessage("用户ID不能为空")
            .Must(x => x.All(id => id > 0)).WithMessage("用户ID必须大于0");
    }
}
