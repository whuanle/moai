using FastEndpoints;
using FluentValidation;
using MoAI.Plugin.CustomPlugins.Commands;

namespace MoAI.Plugin.Validators;

/// <summary>
/// DeletePluginCommandValidator.
/// </summary>
public class DeletePluginCommandValidator : AbstractValidator<DeletePluginCommand>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DeletePluginCommandValidator"/> class.
    /// </summary>
    public DeletePluginCommandValidator()
    {
        RuleFor(x => x.PluginId).GreaterThan(0).WithMessage("插件id不正确.");
    }
}
