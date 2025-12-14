namespace MoAI.Wiki.DocumentManager;

public class PreloadWikiDocumentResponse
{
    /// <summary>
    /// 文件已存在,如果文件已存在则直接使用 FileId，无需再次上传.
    /// </summary>
    public bool IsExist { get; set; }

    /// <summary>
    /// 文件ID.
    /// </summary>
    public int FileId { get; set; }

    /// <summary>
    /// 预签名上传地址，当 IsExist = true 时字段为空.
    /// </summary>
    public Uri? UploadUrl { get; set; } = default!;

    /// <summary>
    /// 签名过期时间，当 IsExist = true 时字段为空.
    /// </summary>
    public DateTimeOffset? Expiration { get; set; } = default!;
}