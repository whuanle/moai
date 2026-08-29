namespace MoAI.Storage.Models;

/// <summary>
/// 文件上传结果.
/// </summary>
public class FileUploadResult
{
    /// <summary>
    /// ObjectKey.
    /// </summary>
    public string ObjectKey { get; init; } = string.Empty;

    /// <summary>
    /// 文件 SHA-256.
    /// </summary>
    public string FileSha256 { get; init; } = string.Empty;

    /// <summary>
    /// 文件类型.
    /// </summary>
    public string FileType { get; init; } = string.Empty;

    /// <summary>
    /// 文件 ID.
    /// </summary>
    public long FileId { get; init; }
}
