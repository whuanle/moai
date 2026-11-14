namespace MoAI.AIPlugin.Models;

/// <summary>
/// 插件已存储文件的信息.
/// </summary>
public class PluginStoredFile
{
    /// <summary>
    /// 文件 id，用于后续获取下载地址.
    /// </summary>
    public long FileId { get; init; }

    /// <summary>
    /// 文件扩展名（含点号），无法识别时为空串.
    /// </summary>
    public string FileExtension { get; init; } = string.Empty;

    /// <summary>
    /// 内容类型（MIME）.
    /// </summary>
    public string ContentType { get; init; } = string.Empty;

    /// <summary>
    /// 文件大小（字节）.
    /// </summary>
    public long FileSize { get; init; }

    /// <summary>
    /// 文件 SHA-256.
    /// </summary>
    public string FileSha256 { get; init; } = string.Empty;

    /// <summary>
    /// 是否为公开文件.
    /// </summary>
    public bool IsPublic { get; init; }

    /// <summary>
    /// 公开访问地址；非公开文件为 null.
    /// </summary>
    public Uri? PublicUrl { get; init; }
}
