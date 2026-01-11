using FluentValidation;
using MediatR;
using MoAI.Infra.Models;
using MoAI.Plugin.CustomPlugins.Queries.Responses;
using MoAI.Plugin.Models;
using MoAI.Plugin.NativePlugins.Queries.Responses;

namespace MoAI.Plugin.NativePlugins.Queries;

/// <summary>
/// 查询内置插件实例列表.
/// </summary>
public class QueryNativePluginListCommand : IRequest<QueryNativePluginListCommandResponse>, IDynamicOrderable, IModelValidator<QueryNativePluginListCommand>
{
    /// <summary>
    /// 名称搜索.
    /// </summary>
    public string? Keyword { get; init; }

    /// <summary>
    /// 自定义分类 id.
    /// </summary>
    public int? ClassifyId { get; init; }

    /// <summary>
    /// 是否公开.
    /// </summary>
    public bool? IsPublic { get; init; } = default!;

    /// <summary>
    /// 模板分类.
    /// </summary>
    public NativePluginClassify? TemplatePluginClassify { get; init; }

    /// <summary>
    /// 可以使用 TemplatePluginKey、PluginName、Title 字段做排序.
    /// </summary>
    public IReadOnlyCollection<KeyValueBool> OrderByFields { get; init; } = Array.Empty<KeyValueBool>();

    private static readonly HashSet<string> allowedFields = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        nameof(NativePluginInfo.TemplatePluginKey),
        nameof(NativePluginInfo.PluginName),
        nameof(NativePluginInfo.Title)
    };

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<QueryNativePluginListCommand> validate)
    {
        validate.RuleFor(x => x.OrderByFields)
            .Must(fields =>
            {
                return fields.All(field => allowedFields.Contains(field.Key));
            })
            .WithMessage("只支持排序 TemplatePluginKey、PluginName、Title.");
    }
}
