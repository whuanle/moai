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
/// 文档内容.
/// </summary>
public partial class WikiDocumentContentEntity : IFullAudited
{
    /// <summary>
    /// 切片唯一ID（slice_id）.
    /// </summary>
    public long Id { get; set; }

    /// <summary>
    /// 关联知识库标识（冗余字段）.
    /// </summary>
    public int WikiId { get; set; }

    /// <summary>
    /// 关联文档唯一标识.
    /// </summary>
    public int DocumentId { get; set; }

    /// <summary>
    /// 内容，markdown.
    /// </summary>
    public string Content { get; set; } = default!;

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
    /// 软删除.
    /// </summary>
    public long IsDeleted { get; set; }
}
