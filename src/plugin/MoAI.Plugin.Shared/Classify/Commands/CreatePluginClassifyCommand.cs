using FluentValidation;
using MediatR;
using MoAI.Infra.Models;

namespace MoAI.Plugin.Classify.Commands;

/// <summary>
/// 创建插件分类.
/// </summary>
public class CreatePluginClassifyCommand : IRequest<SimpleInt>, IModelValidator<CreatePluginClassifyCommand>
{
    /// <summary>
    /// 分类名称.
    /// </summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// 分类描述.
    /// </summary>
    public string Description { get; init; } = string.Empty;

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<CreatePluginClassifyCommand> validate)
    {
        validate.RuleFor(x => x.Name).NotEmpty().WithMessage("分类名称不能为空.")
            .MaximumLength(10).WithMessage("分类名称不能超过10个字符.");

        validate.RuleFor(x => x.Description)
            .NotEmpty().WithMessage("分类描述不能为空.")
            .MaximumLength(255).WithMessage("分类描述不能超过255个字符.");
    }
}