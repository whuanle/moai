using FluentValidation;
using MediatR;
using MoAI.Infra.Models;
using MoAI.Plugin.Models;

namespace MoAI.Plugin.CustomPlugins.Commands;
/// <summary>
/// 更新 MCP 插件.
/// </summary>
public class UpdateTeamMcpServerPluginCommand : McpServerPluginConnectionOptions, IRequest<EmptyCommandResponse>, IModelValidator<UpdateTeamMcpServerPluginCommand>
{
    /// <summary>
    ///  插件 id.
    /// </summary>
    public int PluginId { get; init; }

    /// <summary>
    /// 团队 id.
    /// </summary>
    public int TeamId { get; init; }

    /// <summary>
    /// 分类 id.
    /// </summary>
    public int ClassifyId { get; init; }

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<UpdateTeamMcpServerPluginCommand> validate)
    {
        validate.RuleFor(x => x.TeamId)
            .NotEmpty().WithMessage("团队id不正确.")
            .GreaterThan(0).WithMessage("团队id不正确.");

        validate.RuleFor(x => x.PluginId)
            .NotEmpty().WithMessage("插件不正确.")
            .GreaterThan(0).WithMessage("插件不正确.");

        validate.RuleFor(x => x.Name)
            .NotEmpty().WithMessage("插件名称长度在 2-30 之间.")
            .Length(2, 30).WithMessage("插件名称长度在 2-30 之间.")
            .Matches("^[a-zA-Z_]+$").WithMessage("插件名称只能包含字母下划线.");

        validate.RuleFor(x => x.Title)
            .NotEmpty().WithMessage("插件名称不能为空.")
            .Length(2, 20).WithMessage("插件名称长度在 2-20 之间.");

        validate.RuleFor(x => x.Description)
            .NotEmpty().WithMessage("插件描述长度在 2-255 之间.")
            .Length(2, 255).WithMessage("插件描述长度在 2-255 之间.");

        validate.RuleFor(x => x.ServerUrl)
            .NotEmpty().WithMessage("MCP Service 地址不能为空.");
    }
}