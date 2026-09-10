namespace MoAI.Wiki.Commands;

/// <summary>
/// 预上传知识库文档响应.
/// </summary>
public class PreUploadWikiDocumentCommandResponse
{
    /// <summary>
    /// 文件 id.
    /// </summary>
    public long FileId { get; set; }

    /// <summary>
    /// 文件是否已存在，如已存在则无需再次上传.
    /// </summary>
    public bool IsExist { get; set; }

    /// <summary>
    /// 预签名上传地址，当 IsExist = true 时为空.
    /// </summary>
    public Uri? UploadUrl { get; set; }

    /// <summary>
    /// 签名过期时间，当 IsExist = true 时为空.
    /// </summary>
    public DateTimeOffset? Expiration { get; init; }
}
