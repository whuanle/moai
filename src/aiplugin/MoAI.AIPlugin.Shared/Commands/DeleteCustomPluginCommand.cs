using FluentValidation;
using MediatR;
using MoAI.Infra.Models;

namespace MoAI.AIPlugin.Commands;

/// <summary>
/// 删除自定义插件.
/// </summary>
public class DeleteCustomPluginCommand : IRequest<EmptyCommandResponse>, IModelValidator<DeleteCustomPluginCommand>
{
    /// <summary>
    /// 插件 id.
    /// </summary>
    public Guid PluginId { get; init; }

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<DeleteCustomPluginCommand> validate)
    {
        validate.RuleFor(x => x.PluginId)
            .NotEmpty().WithMessage("插件id不正确.")
            .NotEqual(Guid.Empty).WithMessage("插件id不正确.");
    }
}
