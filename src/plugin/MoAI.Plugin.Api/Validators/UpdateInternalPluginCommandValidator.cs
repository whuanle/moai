using FastEndpoints;
using FluentValidation;
using MoAI.Plugin.BuiltCommands;
using MoAI.Plugin.Commands;

namespace MoAI.Plugin.Validators;

/// <summary>
/// UpdateInternalPluginCommandValidator.
/// </summary>
public class UpdateInternalPluginCommandValidator : Validator<UpdateInternalPluginCommand>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="UpdateInternalPluginCommandValidator"/> class.
    /// </summary>
    public UpdateInternalPluginCommandValidator()
    {
        RuleFor(x => x.ClassifyId).NotEmpty().WithMessage("分类id不正确.");

        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("插件名称长度在 2-30 之间.")
            .Length(2, 30).WithMessage("插件名称长度在 2-30 之间.")
            .Matches("^[a-zA-Z_]+$").WithMessage("插件名称只能包含字母下划线.");

        RuleFor(x => x.Description).MaximumLength(255).WithMessage("分类描述不能超过255个字符.");
        RuleFor(x => x.Config).NotEmpty().WithMessage("参数不能为空.");
    }
}
