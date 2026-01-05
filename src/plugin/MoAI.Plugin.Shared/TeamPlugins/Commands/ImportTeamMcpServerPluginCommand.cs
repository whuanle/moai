using FluentValidation;
using MediatR;
using MoAI.Infra.Models;
using MoAI.Plugin.Models;

namespace MoAI.Plugin.TeamPlugins.Commands;

/// <summary>
/// 团队导入 MCP 服务插件.
/// </summary>
public class ImportTeamMcpServerPluginCommand : McpServerPluginConnectionOptions, IRequest<SimpleInt>, IModelValidator<ImportTeamMcpServerPluginCommand>
{
    /// <summary>
    /// 团队 ID.
    /// </summary>
    public int TeamId { get; init; }

    /// <summary>
    /// 分类 ID.
    /// </summary>
    public int ClassifyId { get; init; }

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<ImportTeamMcpServerPluginCommand> validate)
    {
        validate.RuleFor(x => x.TeamId)
            .GreaterThan(0).WithMessage("团队 ID 必须大于 0.");

        validate.RuleFor(x => x.Name)
            .NotEmpty().WithMessage("插件名称长度在 2-30 之间.")
            .Length(2, 30).WithMessage("插件名称长度在 2-30 之间.")
            .Matches("^[a-zA-Z_]+$").WithMessage("插件名称只能包含字母下划线.");

        validate.RuleFor(x => x.Title)
            .NotEmpty().WithMessage("插件标题不能为空.")
            .Length(2, 20).WithMessage("插件标题长度在 2-20 之间.");

        validate.RuleFor(x => x.Description)
            .NotEmpty().WithMessage("插件描述长度在 2-255 之间.")
            .Length(2, 255).WithMessage("插件描述长度在 2-255 之间.");

        validate.RuleFor(x => x.ServerUrl)
            .NotEmpty().WithMessage("MCP Service 地址不能为空.");
    }
}
