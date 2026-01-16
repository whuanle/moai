using FluentValidation;
using MediatR;
using MoAI.Infra.Models;

namespace MoAI.App.Manager.ManagerApp.Commands;

/// <summary>
/// 删除应用.
/// </summary>
public class DeleteAppCommand : IRequest<EmptyCommandResponse>, IModelValidator<DeleteAppCommand>
{
    /// <summary>
    /// 团队id.
    /// </summary>
    public int TeamId { get; init; }

    /// <summary>
    /// 应用id.
    /// </summary>
    public Guid AppId { get; init; }

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<DeleteAppCommand> validate)
    {
        validate.RuleFor(x => x.TeamId)
            .GreaterThan(0).WithMessage("团队id不能为空.");

        validate.RuleFor(x => x.AppId)
            .NotEmpty().WithMessage("应用id不能为空.");
    }
}
