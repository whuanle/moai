using FastEndpoints;
using FluentValidation;
using MoAI.Plugin.CustomPlugins.Commands;

namespace MoAI.Plugin.Validators;

/// <summary>
/// ImportOpenApiPluginCommandValidator.
/// </summary>
public class ImportOpenApiPluginCommandValidator : AbstractValidator<ImportOpenApiPluginCommand>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ImportOpenApiPluginCommandValidator"/> class.
    /// </summary>
    public ImportOpenApiPluginCommandValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("插件名称长度在 2-30 之间.")
            .Length(2, 30).WithMessage("插件名称长度在 2-30 之间.")
            .Matches("^[a-zA-Z_]+$").WithMessage("插件名称只能包含字母下划线.");

        RuleFor(x => x.Title)
            .NotEmpty().WithMessage("插件名称不能为空.")
            .Length(2, 20).WithMessage("插件名称长度在 2-20 之间.");

        RuleFor(x => x.Description)
            .NotEmpty().WithMessage("插件描述长度在 2-255 之间.")
            .Length(2, 255).WithMessage("插件描述长度在 2-255 之间.");

        RuleFor(x => x.FileName)
            .NotEmpty().WithMessage("文件名称长度在 2-100 之间.")
            .Length(2, 100).WithMessage("文件名称长度在 2-100 之间.");

        RuleFor(x => x.FileId)
            .GreaterThan(0).WithMessage("文件 ID 必须大于 0.");
    }
}
