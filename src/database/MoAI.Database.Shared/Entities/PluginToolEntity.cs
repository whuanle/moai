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
/// 内置插件.
/// </summary>
public partial class PluginToolEntity : IFullAudited
{
    /// <summary>
    /// id.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// 模板分类.
    /// </summary>
    public string TemplatePluginClassify { get; set; } = default!;

    /// <summary>
    /// 对应的内置插件key.
    /// </summary>
    public string TemplatePluginKey { get; set; } = default!;

    /// <summary>
    /// 创建人.
    /// </summary>
    public int CreateUserId { get; set; }

    /// <summary>
    /// 创建时间.
    /// </summary>
    public DateTimeOffset CreateTime { get; set; }

    /// <summary>
    /// 最后修改人.
    /// </summary>
    public int UpdateUserId { get; set; }

    /// <summary>
    /// 最后更新时间.
    /// </summary>
    public DateTimeOffset UpdateTime { get; set; }

    /// <summary>
    /// 软删除.
    /// </summary>
    public long IsDeleted { get; set; }
}
