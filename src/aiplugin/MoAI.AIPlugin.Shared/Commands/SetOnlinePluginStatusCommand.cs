using FluentValidation;
using MediatR;
using MoAI.AIPlugin.Queries.Responses;
using MoAI.Infra.Models;

namespace MoAI.AIPlugin.Commands;

/// <summary>上架或下架已安装在线插件；不提供卸载操作.</summary>
public sealed class SetOnlinePluginStatusCommand : IRequest<InstalledPluginItem>, IModelValidator<SetOnlinePluginStatusCommand>
{
    /// <summary>插件 key.</summary>
    public string PluginKey { get; init; } = string.Empty;

    /// <summary>是否上架.</summary>
    public bool Enabled { get; init; }

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<SetOnlinePluginStatusCommand> validate)
    {
        validate.RuleFor(x => x.PluginKey).NotEmpty().MaximumLength(64);
    }
}
