using FluentValidation;
using MediatR;
using MoAI.AIPlugin.Models;
using MoAI.Infra.Models;

namespace MoAI.AIPlugin.Commands;

/// <summary>
/// 导入 mcp 服务，导入时会访问 mcp 服务器，可能会导致导入比较慢.
/// </summary>
public class ImportMcpServerPluginCommand : McpServerPluginConnectionOptions, IRequest<SimpleGuid>, IModelValidator<ImportMcpServerPluginCommand>
{
    /// <summary>
    /// 分类 id.
    /// </summary>
    public int ClassifyId { get; init; }

    /// <summary>
    /// 是否公开.
    /// </summary>
    public bool IsPublic { get; init; }

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
