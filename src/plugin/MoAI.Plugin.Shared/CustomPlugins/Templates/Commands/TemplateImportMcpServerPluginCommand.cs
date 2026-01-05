using FluentValidation;
using MediatR;
using MoAI.Infra.Models;
using MoAI.Plugin.CustomPlugins.Commands;
using MoAI.Plugin.Models;

namespace MoAI.Plugin.CustomPlugins.Templates.Commands;

/// <summary>
/// 导入 mcp 服务，导入时会访问 mcp 服务器，可能会导致导入比较慢.
/// </summary>
public class TemplateImportMcpServerPluginCommand : McpServerPluginConnectionOptions, IRequest<SimpleInt>, IModelValidator<ImportMcpServerPluginCommand>
{
    /// <summary>
    /// 分类 id.
    /// </summary>
    public int ClassifyId { get; init; }

    /// <summary>
    /// 是否公开，团队的插件不能设置公开.
    /// </summary>
    public bool IsPublic { get; init; } = default!;

    /// <summary>
    /// 团队 id.
    /// </summary>
    public int? TeamId { get; init; }

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<ImportMcpServerPluginCommand> validate)
    {
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
