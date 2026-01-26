using FluentValidation;
using MediatR;
using MoAI.Infra.Models;

namespace MoAI.App.Manager.Commands;

/// <summary>
/// 启用禁用应用.
/// </summary>
public class SetAppDisableCommand : IRequest<EmptyCommandResponse>, IModelValidator<SetAppDisableCommand>
{
    /// <summary>
    /// 团队id.
    /// </summary>
    public int TeamId { get; init; }

    /// <summary>
    /// 应用id.
    /// </summary>
    public Guid AppId { get; init; }

    /// <summary>
    /// 是否禁用.
    /// </summary>
    public bool IsDisable { get; init; }

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<SetAppDisableCommand> validate)
    {
        validate.RuleFor(x => x.TeamId)
            .GreaterThan(0).WithMessage("团队id不能为空.");

        validate.RuleFor(x => x.AppId)
            .NotEmpty().WithMessage("应用id不能为空.");
    }
}
