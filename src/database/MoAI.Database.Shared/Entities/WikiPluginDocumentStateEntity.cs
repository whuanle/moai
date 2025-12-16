using MoAI.Database.Audits;

namespace MoAI.Database.Entities;

/// <summary>
/// 知识库文档关联任务.
/// </summary>
public partial class WikiPluginDocumentStateEntity : IFullAudited
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
    /// 爬虫id.
    /// </summary>
    public int ConfigId { get; set; }

    /// <summary>
    /// 文档id.
    /// </summary>
    public int WikiDocumentId { get; set; }

    /// <summary>
    /// 关联对象.
    /// </summary>
    public string RelevanceKey { get; set; } = default!;

    /// <summary>
    /// 关联值.
    /// </summary>
    public string RelevanceValue { get; set; } = default!;

    /// <summary>
    /// 信息.
    /// </summary>
    public string Message { get; set; } = default!;

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
