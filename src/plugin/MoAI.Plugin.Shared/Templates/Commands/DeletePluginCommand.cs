using FluentValidation;
using MediatR;
using MoAI.Infra.Models;

namespace MoAI.Plugin.Templates.Commands;

/// <summary>
/// 删除插件，不能删除工具类插件.
/// </summary>
public class DeletePluginCommand : IRequest<EmptyCommandResponse>, IModelValidator<DeletePluginCommand>
{
    /// <summary>
    /// 插件.
    /// </summary>
    public int PluginId { get; init; }

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<DeletePluginCommand> validate)
    {
        validate.RuleFor(x => x.PluginId).GreaterThan(0).WithMessage("插件id不正确.");
    }
}