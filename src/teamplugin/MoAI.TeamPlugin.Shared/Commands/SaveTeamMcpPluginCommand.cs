using FluentValidation;
using MediatR;
using MoAI.AIPlugin.Models;
using MoAI.Infra.Models;

namespace MoAI.TeamPlugin.Commands;

/// <summary>
/// 导入/更新团队 MCP 服务器插件，导入时会访问 MCP 服务器.
/// </summary>
public class SaveTeamMcpPluginCommand : McpServerPluginConnectionOptions, IUserIdContext, IRequest<SimpleGuid>, IModelValidator<SaveTeamMcpPluginCommand>
{
    /// <inheritdoc/>
    public long ContextUserId { get; init; }

    /// <inheritdoc/>
    public UserType ContextUserType { get; init; }

    /// <summary>
    /// 团队 id.
    /// </summary>
    public long TeamId { get; init; }

    /// <summary>
    /// 插件记录 id；更新时传入，新建为空.
    /// </summary>
    public Guid? PluginId { get; init; }

    /// <summary>
    /// 分类 id，0 表示未分类.
    /// </summary>
    public int ClassifyId { get; init; }

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<SaveTeamMcpPluginCommand> validate)
    {
        validate.RuleFor(x => x.TeamId).GreaterThan(0).WithMessage("团队 id 不正确.");
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
