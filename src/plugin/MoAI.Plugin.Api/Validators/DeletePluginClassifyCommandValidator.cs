using FastEndpoints;
using FluentValidation;
using MoAI.Plugin.Commands;

namespace MoAI.Plugin.Validators;

/// <summary>
/// DeletePluginClassifyCommandValidator.
/// </summary>
public class DeletePluginClassifyCommandValidator : Validator<DeletePluginClassifyCommand>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DeletePluginClassifyCommandValidator"/> class.
    /// </summary>
    public DeletePluginClassifyCommandValidator()
    {
        RuleFor(x => x.ClassifyId).NotEmpty().WithMessage("分类id不正确.");
    }
}
