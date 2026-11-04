using System;
using System.Collections.Generic;
using FluentValidation;
using MediatR;
using MoAI.Infra.Models;

namespace MoAI.AIPlugin.Commands;

/// <summary>
/// 保存插件的语义配置（整体覆盖）：插件级使用须知、语义条目与工具元数据.
/// </summary>
public class SavePluginSemanticCommand : IRequest<EmptyCommandResponse>, IModelValidator<SavePluginSemanticCommand>
{
    /// <summary>
    /// 插件记录 id（plugin.id）.
    /// </summary>
    public Guid PluginId { get; init; }

    /// <summary>
    /// 插件级使用须知/领域背景（随工具清单注入模型系统提示），≤2000 字符，空串清除.
    /// </summary>
    public string UsageNotes { get; init; } = string.Empty;

    /// <summary>
    /// 语义条目列表（整体覆盖：不在列表内的既有条目将被删除），≤50 条.
    /// </summary>
    public List<SavePluginSemanticNoteItem> Notes { get; init; } = new();

    /// <summary>
    /// 工具元数据列表（整体覆盖：按 toolKey upsert，不在列表内的既有元数据将被删除），≤100 条.
    /// </summary>
    public List<SavePluginToolMetaItem> ToolMetas { get; init; } = new();

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<SavePluginSemanticCommand> validate)
    {
        // PluginId 由 Controller 经路由 {id} 回填（自动验证发生在回填之前，不可在此校验——oauthconnect 实踩坑）；
        // id 缺失时 Handler 查不到插件统一 404
        validate.RuleFor(x => x.UsageNotes).MaximumLength(2000).WithMessage("使用须知长度不能超过 2000 字符");
        validate.RuleFor(x => x.Notes.Count).LessThanOrEqualTo(50).WithMessage("语义条目数量不能超过 50");
        validate.RuleFor(x => x.Notes).Must(items => items.All(item =>
                item.Title.Length > 0 && item.Title.Length <= 100 &&
                item.ToolKey.Length <= 200 &&
                item.SortOrder >= 0 &&
                (item.Content.Length <= 4000 || !string.Equals(item.Mode, "always", StringComparison.OrdinalIgnoreCase)) &&
                item.Content.Length <= 12000))
            .WithMessage("条目标题必填且 ≤100 字符、工具 key ≤200 字符、排序非负、内容 always ≤4000 / onDemand ≤12000 字符");
        validate.RuleFor(x => x.Notes).Must(items => items.All(item =>
                string.Equals(item.Mode, "always", StringComparison.OrdinalIgnoreCase)
                || string.Equals(item.Mode, "onDemand", StringComparison.OrdinalIgnoreCase)))
            .WithMessage("条目披露方式仅支持 always / onDemand");
        validate.RuleFor(x => x.ToolMetas.Count).LessThanOrEqualTo(100).WithMessage("工具元数据数量不能超过 100");
        validate.RuleFor(x => x.ToolMetas).Must(items => items.All(item =>
                item.ToolKey.Length > 0 && item.ToolKey.Length <= 200 && item.ExtraNotes.Length <= 2000))
            .WithMessage("工具 key 必填且 ≤200 字符、补充说明 ≤2000 字符");
        validate.RuleForEach(x => x.ToolMetas).Must(item => Models.PluginToolCallPolicy.TryValidate(item.CallPolicy, out _))
            .WithMessage("调用策略 JSON 不合法（denyPatterns 正则错误或 action 非法）");
    }
}

/// <summary>
/// 语义条目保存项.
/// </summary>
public class SavePluginSemanticNoteItem
{
    /// <summary>
    /// 条目 id，更新已有条目时必传，新增为空.
    /// </summary>
    public Guid? Id { get; init; }

    /// <summary>
    /// 工具 key，空串=插件级条目.
    /// </summary>
    public string ToolKey { get; init; } = string.Empty;

    /// <summary>
    /// 条目标题，≤100 字符.
    /// </summary>
    public string Title { get; init; } = string.Empty;

    /// <summary>
    /// 条目内容（markdown），always ≤4000 字符、onDemand ≤12000 字符.
    /// </summary>
    public string Content { get; init; } = string.Empty;

    /// <summary>
    /// 披露方式：always=常驻注入；onDemand=按需经 plugin_note 取回.
    /// </summary>
    public string Mode { get; init; } = "always";

    /// <summary>
    /// 排序.
    /// </summary>
    public int SortOrder { get; init; }
}

/// <summary>
/// 工具元数据保存项.
/// </summary>
public class SavePluginToolMetaItem
{
    /// <summary>
    /// 工具 key，必填 ≤200 字符.
    /// </summary>
    public string ToolKey { get; init; } = string.Empty;

    /// <summary>
    /// 工具补充说明，≤2000 字符.
    /// </summary>
    public string ExtraNotes { get; init; } = string.Empty;

    /// <summary>
    /// 调用策略 JSON（denyPatterns + action：block/confirm/audit），空串=未配置.
    /// </summary>
    public string CallPolicy { get; init; } = string.Empty;

    /// <summary>
    /// 只读提示.
    /// </summary>
    public bool ReadOnlyHint { get; init; }

    /// <summary>
    /// 破坏性提示.
    /// </summary>
    public bool DestructiveHint { get; init; }
}
