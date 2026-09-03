using FluentValidation;
using MediatR;
using MoAI.Infra.Models;

namespace MoAI.Team.Commands;

/// <summary>
/// 创建团队，创建者自动成为 Owner.
/// </summary>
public class CreateTeamCommand : IRequest<SimpleLong>, IModelValidator<CreateTeamCommand>
{
    /// <summary>
    /// 团队名称.
    /// </summary>
    public string Name { get; init; } = default!;

    /// <summary>
    /// 团队简介.
    /// </summary>
    public string? Description { get; init; }

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<CreateTeamCommand> validate)
    {
        validate.RuleFor(x => x.Name).NotEmpty().WithMessage("团队名称不能为空.").MaximumLength(50).WithMessage("团队名称最长 50 个字符.");
        validate.RuleFor(x => x.Description).MaximumLength(255).WithMessage("团队简介最长 255 个字符.");
    }
}
