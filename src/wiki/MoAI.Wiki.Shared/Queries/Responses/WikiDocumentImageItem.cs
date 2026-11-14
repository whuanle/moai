using MoAI.Infra.Models;

namespace MoAI.Wiki.Queries.Responses;

/// <summary>
/// 知识库文档内嵌图片附件项.
/// </summary>
public class WikiDocumentImageItem : AuditsInfo
{
    /// <summary>
    /// 图片附件 id.
    /// </summary>
    public int ImageId { get; set; }

    /// <summary>
    /// 知识库 id.
    /// </summary>
    public int WikiId { get; set; }

    /// <summary>
    /// 文档 id.
    /// </summary>
    public int DocumentId { get; set; }

    /// <summary>
    /// 文件 id.
    /// </summary>
    public long FileId { get; set; }

    /// <summary>
    /// 图片文件名.
    /// </summary>
    public string FileName { get; set; } = default!;

    /// <summary>
    /// 文件扩展名称，如.png.
    /// </summary>
    public string FileType { get; set; } = default!;

    /// <summary>
    /// 文件大小（字节）.
    /// </summary>
    public int FileSize { get; set; }

    /// <summary>
    /// 所在页码（pdf 提取可解析），null=未知.
    /// </summary>
    public int? PageNumber { get; set; }

    /// <summary>
    /// 文档内序号，按提取产出顺序从 0 递增.
    /// </summary>
    public int ImageIndex { get; set; }

    /// <summary>
    /// 预签名下载地址（有效期 2 分钟）.
    /// </summary>
    public string DownloadUrl { get; set; } = default!;
}
