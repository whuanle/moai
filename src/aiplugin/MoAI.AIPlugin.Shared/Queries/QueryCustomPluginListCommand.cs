using FluentValidation;
using MediatR;
using MoAI.AIPlugin.Models;
using MoAI.AIPlugin.Queries.Responses;
using MoAI.Infra.Models;

namespace MoAI.AIPlugin.Queries;

/// <summary>
/// 获取自定义插件插件基础信息列表.
/// </summary>
public class QueryCustomPluginListCommand : IRequest<QueryCustomPluginBaseListCommandResponse>, IModelValidator<QueryCustomPluginListCommand>, IDynamicOrderable
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
    public bool? IsPublic { get; init; }

    /// <summary>
    /// 排序，支持 PluginName、Title、Type 排序.
    /// </summary>
    public IReadOnlyCollection<KeyValueBool> OrderByFields { get; init; } = Array.Empty<KeyValueBool>();

    private static readonly HashSet<string> AllowedFields = new(StringComparer.OrdinalIgnoreCase)
    {
        nameof(PluginBaseInfoItem.PluginName),
        nameof(PluginBaseInfoItem.Title),
        nameof(PluginBaseInfoItem.Type),
    };

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<QueryCustomPluginListCommand> validate)
    {
        validate.RuleFor(x => x.OrderByFields)
            .Must(fields => fields.All(field => AllowedFields.Contains(field.Key)))
            .WithMessage("只支持排序 PluginName、Title、Type.");
    }
}
