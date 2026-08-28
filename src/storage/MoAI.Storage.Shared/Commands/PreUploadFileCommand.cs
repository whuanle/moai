namespace MoAI.Storage.Commands;

/// <summary>
/// 生成文件预上传的输入参数.
/// </summary>
public class PreUploadFileCommand
{
    /// <summary>
    /// 文件类型.
    /// </summary>
    public string ContentType { get; set; } = default!;

    /// <summary>
    /// 文件大小.
    /// </summary>
    public int FileSize { get; set; }

    /// <summary>
    /// 文件 SHA-256.
    /// </summary>
    public string SHA256 { get; set; } = default!;

    /// <summary>
    /// 文件路径，即 ObjectKey.
    /// </summary>
    public string ObjectKey { get; set; } = default!;

    /// <summary>
    /// 预签名上传地址有效期.
    /// </summary>
    public TimeSpan Expiration { get; set; } = TimeSpan.FromMinutes(2);
}
