namespace MoAI.Storage.Commands;

/// <summary>
/// 文件上传结果.
/// </summary>
public class FileUploadResult
{
    /// <summary>
    /// objectkey.
    /// </summary>
    public string ObjectKey { get; init; } = string.Empty;

    /// <summary>
    /// 文件md5.
    /// </summary>
    public string FileMd5 { get; init; } = string.Empty;

    /// <summary>
    /// 文件类型.
    /// </summary>
    public string FileType { get; init; } = string.Empty;

    /// <summary>
    /// 文件 id.
    /// </summary>
    public int FileId { get; init; }
}
