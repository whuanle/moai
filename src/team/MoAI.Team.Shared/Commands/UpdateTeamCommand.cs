using FluentValidation;
using MediatR;
using MoAI.Infra.Models;

namespace MoAI.Team.Commands;

/// <summary>
/// 编辑团队信息命令.
/// </summary>
public class UpdateTeamCommand : IRequest<EmptyCommandResponse>, IModelValidator<UpdateTeamCommand>
{
    /// <summary>
    /// 团队ID.
    /// </summary>
    public int TeamId { get; init; }

    /// <summary>
    /// 团队名称.
    /// </summary>
    public string? Name { get; init; }

    /// <summary>
    /// 团队描述.
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// 团队头像 ObjectKey.
    /// </summary>
    public string? Avatar { get; init; }

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<UpdateTeamCommand> validate)
    {
        validate.RuleFor(x => x.TeamId)
            .GreaterThan(0).WithMessage("团队ID不正确");

        validate.RuleFor(x => x.Name)
            .MaximumLength(100).WithMessage("团队名称不能超过100个字符")
            .When(x => !string.IsNullOrEmpty(x.Name));
    }
}
