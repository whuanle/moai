using FastEndpoints;
using FluentValidation;
using MoAI.Plugin.Commands;

namespace MoAI.Plugin.Validators;

/// <summary>
/// RefreshMcpServerPluginCommandValidator.
/// </summary>
public class RefreshMcpServerPluginCommandValidator : Validator<RefreshMcpServerPluginCommand>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="RefreshMcpServerPluginCommandValidator"/> class.
    /// </summary>
    public RefreshMcpServerPluginCommandValidator()
    {
        RuleFor(x => x.PluginId)
            .NotEmpty().WithMessage("插件id不正确.")
            .GreaterThan(0).WithMessage("插件id不正确.");
    }
}
