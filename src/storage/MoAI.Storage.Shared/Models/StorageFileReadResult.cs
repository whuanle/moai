namespace MoAI.Storage.Models;

/// <summary>
/// 文件流读取结果.
/// </summary>
public class StorageFileReadResult
{
    /// <summary>
    /// 文件流.
    /// </summary>
    public Stream FileStream { get; init; } = default!;

    /// <summary>
    /// 内容类型.
    /// </summary>
    public string ContentType { get; init; } = string.Empty;

    /// <summary>
    /// 文件大小.
    /// </summary>
    public long FileSize { get; init; }

    /// <summary>
    /// 文件扩展名.
    /// </summary>
    public string FileExtension { get; init; } = string.Empty;

    /// <summary>
    /// ObjectKey.
    /// </summary>
    public string ObjectKey { get; init; } = string.Empty;
}
