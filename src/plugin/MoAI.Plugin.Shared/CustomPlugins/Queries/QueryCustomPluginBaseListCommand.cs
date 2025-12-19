using FluentValidation;
using MediatR;
using MoAI.Infra.Models;
using MoAI.Plugin.CustomPlugins.Queries.Responses;
using MoAI.Plugin.Models;

namespace MoAI.Plugin.CustomPlugins.Queries;

/// <summary>
/// 获取自定义插件插件基础信息列表.
/// </summary>
public class QueryCustomPluginBaseListCommand : IRequest<QueryCustomPluginBaseListCommandResponse>, IModelValidator<QueryCustomPluginBaseListCommand>
{
    /// <summary>
    /// 名称搜索.
    /// </summary>
    public string? Name { get; init; }

    /// <summary>
    /// 筛选类型，这里只能使用 mcp 或 openapi，不筛选则不填.
    /// </summary>
    public PluginType? Type { get; init; }

    /// <summary>
    /// 分类 id.
    /// </summary>
    public int? ClassifyId { get; init; }

    /// <summary>
    /// 是否公开.
    /// </summary>
    public bool? IsPublic { get; init; } = default!;

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<QueryCustomPluginBaseListCommand> validate)
    {
    }
}
