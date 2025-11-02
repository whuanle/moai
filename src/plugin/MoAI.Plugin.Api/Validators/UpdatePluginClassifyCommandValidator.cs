using FastEndpoints;
using FluentValidation;
using MoAI.Plugin.BuiltCommands;
using MoAI.Plugin.Commands;

namespace MoAI.Plugin.Validators;

/// <summary>
/// UpdatePluginClassifyCommandValidator.
/// </summary>
public class UpdatePluginClassifyCommandValidator : Validator<UpdatePluginClassifyCommand>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="UpdatePluginClassifyCommandValidator"/> class.
    /// </summary>
    public UpdatePluginClassifyCommandValidator()
    {
        RuleFor(x => x.ClassifyId).NotEmpty().WithMessage("分类id不正确.");

        RuleFor(x => x.Name).NotEmpty().WithMessage("分类名称不能为空.")
            .MaximumLength(10).WithMessage("分类名称不能超过10个字符.");

        RuleFor(x => x.Description).MaximumLength(255).WithMessage("分类描述不能超过255个字符.");
    }
}
