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
/// 插件语义条目：管理员为插件（或其某个工具）维护的结构化语义说明，按披露方式注入 AI 上下文.
/// </summary>
public partial class PluginSemanticNoteEntity : IFullAudited
{
    /// <summary>
    /// id.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// 所属插件 id，逻辑关联 plugin.id.
    /// </summary>
    public Guid PluginId { get; set; }

    /// <summary>
    /// 工具 key，空串=插件级条目；否则为工具 key（MCP/OpenAPI 为函数名，动态插件为工具 key）.
    /// </summary>
    public string ToolKey { get; set; } = default!;

    /// <summary>
    /// 条目标题（按需披露时作为目录展示给模型）.
    /// </summary>
    public string Title { get; set; } = default!;

    /// <summary>
    /// 条目内容（markdown，如库表用途、字段说明、业务术语、示例问答）.
    /// </summary>
    public string Content { get; set; } = default!;

    /// <summary>
    /// 披露方式：0=always 常驻注入上下文；1=on_demand 经 plugin_note 元工具按需取回.
    /// </summary>
    public int Mode { get; set; }

    /// <summary>
    /// 排序.
    /// </summary>
    public int SortOrder { get; set; }

    /// <summary>
    /// 创建人.
    /// </summary>
    public long CreateUserId { get; set; }

    /// <summary>
    /// 创建时间.
    /// </summary>
    public DateTimeOffset CreateTime { get; set; }

    /// <summary>
    /// 最后修改人.
    /// </summary>
    public long UpdateUserId { get; set; }

    /// <summary>
    /// 更新时间.
    /// </summary>
    public DateTimeOffset UpdateTime { get; set; }

    /// <summary>
    /// 软删除.
    /// </summary>
    public long IsDeleted { get; set; }
}
