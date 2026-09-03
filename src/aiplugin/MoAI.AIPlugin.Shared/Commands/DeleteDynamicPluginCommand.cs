using FluentValidation;
using MediatR;
using MoAI.Infra.Models;

namespace MoAI.AIPlugin.Commands;

/// <summary>
/// 删除动态插件实例.
/// </summary>
public class DeleteDynamicPluginCommand : IRequest<EmptyCommandResponse>, IModelValidator<DeleteDynamicPluginCommand>
{
    /// <summary>
    /// 实例 key.
    /// </summary>
    public string PluginKey { get; init; } = string.Empty;

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<DeleteDynamicPluginCommand> validate)
    {
        validate.RuleFor(x => x.PluginKey)
            .NotEmpty().WithMessage("实例 Key 不能为空");
    }
}
