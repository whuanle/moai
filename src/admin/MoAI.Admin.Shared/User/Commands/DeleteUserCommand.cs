using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using MoAI.Infra.Defaults;
using MoAI.Infra.Models;

namespace MoAI.Admin.User.Commands;

/// <summary>
/// 删除用户.
/// </summary>
public class DeleteUserCommand : IRequest<EmptyCommandResponse>, IModelValidator<DeleteUserCommand>
{
    /// <summary>
    /// 用户ID.
    /// </summary>
    public IReadOnlyCollection<int> UserIds { get; init; } = Array.Empty<int>();

    /// <inheritdoc/>
    public void Validate(AbstractValidator<DeleteUserCommand> validate)
    {
        validate.RuleFor(x => x.UserIds)
            .NotEmpty().WithMessage("用户ID不能为空")
            .Must(x => x.Count > 0).WithMessage("用户ID不能为空")
            .Must(x => x.All(id => id > 0)).WithMessage("用户ID必须大于0");
    }
}
