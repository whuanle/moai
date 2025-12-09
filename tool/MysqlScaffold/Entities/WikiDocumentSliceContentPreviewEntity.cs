using System;
using System.Collections.Generic;
using MoAI.Database.Audits;

namespace MoAI.Database.Entities;

/// <summary>
/// 文档切片预览.
/// </summary>
public partial class WikiDocumentSliceContentPreviewEntity : IFullAudited
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
    /// 原始切片内容.
    /// </summary>
    public string SliceContent { get; set; } = default!;

    /// <summary>
    /// 切片在文档中的顺序.
    /// </summary>
    public int SliceOrder { get; set; }

    /// <summary>
    /// 切片字符长度.
    /// </summary>
    public int SliceLength { get; set; }

    /// <summary>
    /// 切片所在页码（无页码填0）.
    /// </summary>
    public int SlicePage { get; set; }

    /// <summary>
    /// 创建人.
    /// </summary>
    public int CreateUserId { get; set; }

    /// <summary>
    /// 创建时间.
    /// </summary>
    public DateTimeOffset CreateTime { get; set; }

    /// <summary>
    /// 更新人.
    /// </summary>
    public int UpdateUserId { get; set; }

    /// <summary>
    /// 更新时间.
    /// </summary>
    public DateTimeOffset UpdateTime { get; set; }

    /// <summary>
    /// 软删除.
    /// </summary>
    public long IsDeleted { get; set; }
}
