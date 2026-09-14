using System;
using System.Collections.Generic;
using MoAI.Database.Audits;

#pragma warning disable CA1051
#pragma warning disable SA1401
#pragma warning disable SA1600
#pragma warning disable SA1601
#pragma warning disable SA1204
namespace MoAI.Database.Entities;

/// <summary>
/// Agent 应用配置，与 app 一一对应（app_type=0）.
/// </summary>
public partial class AppAgentConfigEntity : IFullAudited
{
    /// <summary>
    /// 配置ID.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// 所属团队ID，逻辑关联app.team_id，冗余用于团队维度过滤.
    /// </summary>
    public int TeamId { get; set; }

    /// <summary>
    /// 所属应用ID，逻辑关联app.id（1:1，不建物理外键）.
    /// </summary>
    public Guid AppId { get; set; }

    /// <summary>
    /// 系统提示词，最长4000字符.
    /// </summary>
    public string Prompt { get; set; } = default!;

    /// <summary>
    /// 对话使用的模型ID，逻辑关联ai_model.id（uuid）.
    /// </summary>
    public Guid ModelId { get; set; }

    /// <summary>
    /// 绑定的知识库ID列表，JSON 数组文本，元素为 wiki.id（整数），如 &apos;[1,2]&apos;.
    /// </summary>
    public string WikiIds { get; set; } = default!;

    /// <summary>
    /// 绑定的插件ID列表，JSON 数组文本，元素为 plugin.id（uuid 字符串），如 &apos;[&quot;...&quot;]&apos;.
    /// </summary>
    public string Plugins { get; set; } = default!;

    /// <summary>
    /// 对话影响参数，JSON 对象文本（temperature/topP/maxTokens 等），空为 &apos;{}&apos;.
    /// </summary>
    public string ExecutionSettings { get; set; } = default!;

    /// <summary>
    /// 创建人.
    /// </summary>
    public long CreateUserId { get; set; }

    /// <summary>
    /// 创建时间.
    /// </summary>
    public DateTimeOffset CreateTime { get; set; }

    /// <summary>
    /// 更新人.
    /// </summary>
    public long UpdateUserId { get; set; }

    /// <summary>
    /// 更新时间.
    /// </summary>
    public DateTimeOffset UpdateTime { get; set; }

    /// <summary>
    /// 软删除，0=未删除（legacy bigint 约定）.
    /// </summary>
    public long IsDeleted { get; set; }

    /// <summary>
    /// 绑定的技能ID列表，JSON 数组文本，元素为 skill.id（uuid 字符串），如 [&quot;...&quot;].
    /// </summary>
    public string Skills { get; set; } = default!;
}
