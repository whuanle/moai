namespace MoAI.Storage.Models;

/// <summary>
/// 文件信息.
/// </summary>
public class StorageFileInfo
{
    /// <summary>
    /// 文件 ID.
    /// </summary>
    public int FileId { get; init; }

    /// <summary>
    /// ObjectKey.
    /// </summary>
    public string ObjectKey { get; init; } = default!;

    /// <summary>
    /// 文件扩展名.
    /// </summary>
    public string FileExtension { get; init; } = default!;

    /// <summary>
    /// 文件 SHA-256.
    /// </summary>
    public string FileSha256 { get; init; } = default!;

    /// <summary>
    /// 文件大小.
    /// </summary>
    public int FileSize { get; init; }

    /// <summary>
    /// 内容类型.
    /// </summary>
    public string ContentType { get; init; } = default!;
}
