using FluentValidation;
using MediatR;
using MoAI.AIPlugin.Queries.Responses;
using MoAI.Infra.Models;

namespace MoAI.AIPlugin.Commands;

/// <summary>安装一个 MoPlatform 在线插件.</summary>
public sealed class InstallOnlinePluginCommand : IRequest<InstalledPluginItem>, IModelValidator<InstallOnlinePluginCommand>
{
    /// <summary>MoPlatform 插件 id.</summary>
    public long PlatformPluginId { get; init; }

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<InstallOnlinePluginCommand> validate)
    {
        validate.RuleFor(x => x.PlatformPluginId).GreaterThan(0);
    }
}
