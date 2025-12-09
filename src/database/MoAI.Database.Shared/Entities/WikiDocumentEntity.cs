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
    /// 是否已经向量化.
    /// </summary>
    public bool IsEmbedding { get; set; }

    /// <summary>
    /// 版本号，可与向量元数据对比，确认最新文档版本号是否一致.
    /// </summary>
    public long VersionNo { get; set; }
    public string SpliceConfig { get; set; }

    /// <summary>
    /// 是否有更新，需要重新进行向量化.
    /// </summary>
    public bool IsUpdate { get; set; }

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
