using System;
using System.Collections.Generic;
using MoAI.Database.Audits;

namespace MoAI.Database.Entities;

/// <summary>
/// 插件.
/// </summary>
public partial class PluginCustomEntity : IFullAudited
{
    /// <summary>
    /// id.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// 服务器地址.
    /// </summary>
    public string Server { get; set; } = default!;

    /// <summary>
    /// 头部.
    /// </summary>
    public string Headers { get; set; } = default!;

    /// <summary>
    /// query参数.
    /// </summary>
    public string Queries { get; set; } = default!;

    /// <summary>
    /// mcp|openapi.
    /// </summary>
    public int Type { get; set; }

    /// <summary>
    /// 文件id.
    /// </summary>
    public int OpenapiFileId { get; set; }

    /// <summary>
    /// 文件名称.
    /// </summary>
    public string OpenapiFileName { get; set; } = default!;

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
