namespace MoAI.Storage.Commands.Response;

/// <summary>
/// 临时文件预上传响应.
/// </summary>
public class PreUploadTempFileCommandResponse
{
    /// <summary>
    /// 文件是否已存在，如果已存在则无需再次上传.
    /// </summary>
    public bool IsExist { get; init; }

    /// <summary>
    /// 文件ID.
    /// </summary>
    public int FileId { get; init; }

    /// <summary>
    /// 文件 ObjectKey.
    /// </summary>
    public string ObjectKey { get; init; } = default!;

    /// <summary>
    /// 预签名上传地址，当 IsExist = true 时为空.
    /// </summary>
    public Uri? UploadUrl { get; init; }

    /// <summary>
    /// 签名过期时间，当 IsExist = true 时为空.
    /// </summary>
    public DateTimeOffset? Expiration { get; init; }
}
