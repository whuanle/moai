using FluentValidation;
using MediatR;
using MoAI.App.Manager.ManagerApp.Models;
using MoAI.App.Models;
using MoAI.Infra.Models;

namespace MoAI.App.Manager.ManagerApp.Commands;

/// <summary>
/// 创建应用.
/// </summary>
public class CreateAppCommand : IRequest<CreateAppCommandResponse>, IModelValidator<CreateAppCommand>
{
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
    /// 是否外部应用.
    /// </summary>
    public bool IsForeign { get; init; }

    /// <summary>
    /// 应用类型.
    /// </summary>
    public AppType AppType { get; init; } = AppType.Common;

    /// <summary>
    /// 分类id.
    /// </summary>
    public int ClassifyId { get; init; }

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<CreateAppCommand> validate)
    {
        validate.RuleFor(x => x.TeamId)
            .GreaterThan(0).WithMessage("团队id不能为空.");

        validate.RuleFor(x => x.Name)
            .NotEmpty().WithMessage("应用名称不能为空.")
            .MaximumLength(20).WithMessage("应用名称长度不能超过100个字符.");

        validate.RuleFor(x => x.Description)
            .MaximumLength(255).WithMessage("描述长度不能超过500个字符.");
    }
}
