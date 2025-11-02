using System;
using System.Collections.Generic;
using MoAI.Database.Audits;

namespace MoAI.Database.Entities;

/// <summary>
/// 知识库文档.
/// </summary>
public partial class WikiDocumentEntity : IFullAudited
{
    /// <summary>
    /// id.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// 知识库id.
    /// </summary>
    public int WikiId { get; set; }

    /// <summary>
    /// 文件id.
    /// </summary>
    public int FileId { get; set; }

    /// <summary>
    /// 文件路径.
    /// </summary>
    public string ObjectKey { get; set; } = default!;

    /// <summary>
    /// 文档名称.
    /// </summary>
    public string FileName { get; set; } = default!;

    /// <summary>
    /// 文件扩展名称，如.md.
    /// </summary>
    public string FileType { get; set; } = default!;

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
