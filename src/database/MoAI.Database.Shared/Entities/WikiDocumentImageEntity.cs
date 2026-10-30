using System;
using MoAI.Database.Audits;

#pragma warning disable CA1051
#pragma warning disable SA1401
#pragma warning disable SA1600
#pragma warning disable SA1601
#pragma warning disable SA1204
namespace MoAI.Database.Entities;

/// <summary>
/// 知识库文档内嵌图片附件，文档提取时落出的图片资产登记.
/// </summary>
public partial class WikiDocumentImageEntity : IFullAudited
{
    /// <summary>
    /// 自增主键.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// 知识库id.
    /// </summary>
    public int WikiId { get; set; }

    /// <summary>
    /// 知识库文档id.
    /// </summary>
    public int DocumentId { get; set; }

    /// <summary>
    /// 文件id，图片本体在对象存储，按内容寻址可被多文档共享.
    /// </summary>
    public long FileId { get; set; }

    /// <summary>
    /// 文件路径，wiki/{wikiId}/image/{sha256}.{ext}.
    /// </summary>
    public string ObjectKey { get; set; } = default!;

    /// <summary>
    /// 图片文件名，取自文档内部部件名或提取序名.
    /// </summary>
    public string FileName { get; set; } = default!;

    /// <summary>
    /// 文件扩展名称，如.png.
    /// </summary>
    public string FileType { get; set; } = default!;

    /// <summary>
    /// 所在页码（pdf 提取可解析），null=未知.
    /// </summary>
    public int? PageNumber { get; set; }

    /// <summary>
    /// 文档内序号，按提取产出顺序从 0 递增.
    /// </summary>
    public int ImageIndex { get; set; }

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
