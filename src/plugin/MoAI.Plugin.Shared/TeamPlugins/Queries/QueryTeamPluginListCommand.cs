using FluentValidation;
using MediatR;
using MoAI.AI.Models;
using MoAI.Infra.Models;
using MoAI.Plugin.TeamPlugins.Queries.Responses;

namespace MoAI.Plugin.TeamPlugins.Queries;

/// <summary>
/// 查询团队插件列表.
/// </summary>
public class QueryTeamPluginListCommand : IUserIdContext, IRequest<QueryTeamPluginListCommandResponse>, IModelValidator<QueryTeamPluginListCommand>
{
    /// <inheritdoc/>
    public int ContextUserId { get; init; }

    /// <inheritdoc/>
    public UserType ContextUserType { get; init; }

    /// <summary>
    /// 团队 ID.
    /// </summary>
    public int TeamId { get; init; }

    /// <summary>
    /// 名称搜索.
    /// </summary>
    public string? Name { get; init; }

    /// <summary>
    /// 筛选类型，只能使用 MCP 或 OpenAPI.
    /// </summary>
    public PluginType? Type { get; init; }

    /// <summary>
    /// 分类 ID.
    /// </summary>
    public int? ClassifyId { get; init; }

    /// <summary>
    /// 是否公开.
    /// </summary>
    public bool? IsPublic { get; init; }

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<QueryTeamPluginListCommand> validate)
    {
        validate.RuleFor(x => x.TeamId)
            .GreaterThan(0).WithMessage("团队 ID 必须大于 0.");
    }
}
