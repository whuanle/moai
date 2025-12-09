using FluentValidation;
using MediatR;
using MoAI.Infra.Models;

namespace MoAI.Plugin.CustomPlugins.Commands;

/// <summary>
/// 刷新 MCP 服务器的工具列表.
/// </summary>
public class RefreshMcpServerPluginCommand : IRequest<EmptyCommandResponse>, IModelValidator<RefreshMcpServerPluginCommand>
{
    /// <summary>
    ///  插件 id.
    /// </summary>
    public int PluginId { get; init; }

    /// <inheritdoc/>
    public void Validate(AbstractValidator<RefreshMcpServerPluginCommand> validate)
    {
        validate.RuleFor(x => x.PluginId)
            .NotEmpty().WithMessage("插件id不正确.")
            .GreaterThan(0).WithMessage("插件id不正确.");
    }
}
