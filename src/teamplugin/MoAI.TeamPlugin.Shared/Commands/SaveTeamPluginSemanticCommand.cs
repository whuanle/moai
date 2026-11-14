using System;
using System.Collections.Generic;
using FluentValidation;
using MediatR;
using MoAI.AIPlugin.Commands;
using MoAI.Infra.Models;

namespace MoAI.TeamPlugin.Commands;

/// <summary>
/// 保存团队自有插件的语义配置（整体覆盖），仅团队 Owner/Admin 可操作.
/// </summary>
public class SaveTeamPluginSemanticCommand : IRequest<EmptyCommandResponse>, IModelValidator<SaveTeamPluginSemanticCommand>, IUserIdContext
{
    /// <inheritdoc/>
    public long ContextUserId { get; init; }

    /// <inheritdoc/>
    public UserType ContextUserType { get; init; }

    /// <summary>
    /// 团队 id，由路由回填（自动验证先于回填，不可在此校验）.
    /// </summary>
    public long TeamId { get; init; }

    /// <summary>
    /// 插件记录 id，由路由回填.
    /// </summary>
    public Guid PluginId { get; init; }

    /// <summary>
    /// 插件级使用须知/领域背景，≤2000 字符，空串清除.
    /// </summary>
    public string UsageNotes { get; init; } = string.Empty;

    /// <summary>
    /// 语义条目列表（整体覆盖），≤50 条.
    /// </summary>
    public List<SavePluginSemanticNoteItem> Notes { get; init; } = new();

    /// <summary>
    /// 工具元数据列表（整体覆盖），≤100 条.
    /// </summary>
    public List<SavePluginToolMetaItem> ToolMetas { get; init; } = new();

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<SaveTeamPluginSemanticCommand> validate)
    {
        // TeamId/PluginId 由路由回填，缺失时 Handler 统一 404
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
        validate.RuleForEach(x => x.ToolMetas).Must(item => MoAI.AIPlugin.Models.PluginToolCallPolicy.TryValidate(item.CallPolicy, out _))
            .WithMessage("调用策略 JSON 不合法（denyPatterns 正则错误或 action 非法）");
    }
}
