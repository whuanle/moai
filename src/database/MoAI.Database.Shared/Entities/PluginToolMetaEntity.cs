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
/// 插件工具元数据：按（插件, 工具 key）粒度维护工具补充说明、调用策略与 MCP 行为注解，与插件类型解耦.
/// </summary>
public partial class PluginToolMetaEntity : IFullAudited
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
    /// 工具 key（MCP/OpenAPI 为函数名，动态插件为工具 key，静态插件为 run）.
    /// </summary>
    public string ToolKey { get; set; } = default!;

    /// <summary>
    /// 工具补充说明（叠加在原始描述之上进入模型工具说明）.
    /// </summary>
    public string ExtraNotes { get; set; } = default!;

    /// <summary>
    /// 调用策略 JSON（denyPatterns 正则数组 + action：block|confirm|audit），空串=未配置.
    /// </summary>
    public string CallPolicy { get; set; } = default!;

    /// <summary>
    /// 只读提示（MCP readOnlyHint 采集或人工标注，不构成安全保证）.
    /// </summary>
    public bool ReadOnlyHint { get; set; }

    /// <summary>
    /// 破坏性提示（MCP destructiveHint 采集或人工标注，不构成安全保证）.
    /// </summary>
    public bool DestructiveHint { get; set; }

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
