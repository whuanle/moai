namespace MoAI.Storage.Commands.Response;

/// <summary>
/// 预上传文件.
/// </summary>
public class PreUploadFileCommandResponse
{
    /// <summary>
    /// 已存在相同的 objectKey,如果文件已存在则直接使用 FileId，无需再次上传.
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
