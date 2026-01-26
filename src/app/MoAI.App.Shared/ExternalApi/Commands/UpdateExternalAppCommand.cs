using FluentValidation;
using MediatR;
using MoAI.Infra.Models;

namespace MoAI.App.Manager.ExternalApi.Commands;

/// <summary>
/// 修改系统接入信息.
/// </summary>
public class UpdateExternalAppCommand : IUserIdContext, IRequest<EmptyCommandResponse>, IModelValidator<UpdateExternalAppCommand>
{
    /// <inheritdoc/>
    public int ContextUserId { get; init; }

    /// <inheritdoc/>
    public UserType ContextUserType { get; init; }

    /// <summary>
    /// 团队id.
    /// </summary>
    public int TeamId { get; init; }

    /// <summary>
    /// 应用名称.
    /// </summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// 描述.
    /// </summary>
    public string Description { get; init; } = string.Empty;

    /// <summary>
    /// 头像.
    /// </summary>
    public string Avatar { get; init; } = string.Empty;

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<UpdateExternalAppCommand> validate)
    {
        validate.RuleFor(x => x.TeamId)
            .GreaterThan(0).WithMessage("团队id不能为空.");

        validate.RuleFor(x => x.Name)
            .NotEmpty().WithMessage("应用名称不能为空.")
            .MaximumLength(100).WithMessage("应用名称长度不能超过100个字符.");

        validate.RuleFor(x => x.Description)
            .MaximumLength(500).WithMessage("描述长度不能超过500个字符.");
    }
}
