namespace MoAI.Storage.Commands;

/// <summary>
/// 以流的方式上传文件的输入参数.
/// </summary>
public class UploadStreamFileCommand
{
    /// <summary>
    /// 文件流.
    /// </summary>
    public Stream FileStream { get; init; } = Stream.Null;

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
}
