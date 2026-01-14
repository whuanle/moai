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
/// 插件.
/// </summary>
public partial class PluginEntity : IFullAudited
{
    /// <summary>
    /// id.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// 某个团队创建的自定义插件.
    /// </summary>
    public int TeamId { get; set; }

    /// <summary>
    /// 对应的实际插件的id，不同类型的插件表不一样.
    /// </summary>
    public int PluginId { get; set; }

    /// <summary>
    /// 插件名称.
    /// </summary>
    public string PluginName { get; set; } = default!;

    /// <summary>
    /// 插件标题.
    /// </summary>
    public string Title { get; set; } = default!;

    /// <summary>
    /// 注释.
    /// </summary>
    public string Description { get; set; } = default!;

    /// <summary>
    /// mcp|openapi|native|tool.
    /// </summary>
    public int Type { get; set; }

    /// <summary>
    /// 分类id.
    /// </summary>
    public int ClassifyId { get; set; }

    /// <summary>
    /// 公开访问.
    /// </summary>
    public bool IsPublic { get; set; }

    /// <summary>
    /// 计数器.
    /// </summary>
    public int Counter { get; set; }

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
