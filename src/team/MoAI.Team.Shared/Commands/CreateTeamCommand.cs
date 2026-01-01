using FluentValidation;
using MediatR;
using MoAI.Infra.Models;

namespace MoAI.Team.Commands;

/// <summary>
/// 创建团队命令.
/// </summary>
public class CreateTeamCommand : IRequest<CreateTeamCommandResponse>, IModelValidator<CreateTeamCommand>, IUserIdContext
{
    /// <summary>
    /// 团队名称.
    /// </summary>
    public string Name { get; init; } = default!;

    /// <summary>
    /// 团队描述.
    /// </summary>
    public string Description { get; init; } = string.Empty;

    /// <summary>
    /// 团队头像 ObjectKey.
    /// </summary>
    public string? Avatar { get; init; }

    /// <inheritdoc/>
    public int ContextUserId { get; init; }

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<CreateTeamCommand> validate)
    {
        validate.RuleFor(x => x.Name)
            .NotEmpty().WithMessage("团队名称不能为空")
            .MaximumLength(100).WithMessage("团队名称不能超过100个字符");
    }
}
