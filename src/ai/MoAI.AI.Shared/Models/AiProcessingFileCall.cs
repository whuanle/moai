namespace MoAI.AI.Models;

/// <summary>
/// 文件调用.
/// </summary>
public interface AiProcessingFileCall
{
    /// <summary>
    /// 文本 key.
    /// </summary>
    public string? FileKey { get; }

    /// <summary>
    /// 文件地址.
    /// </summary>
    public string? FileUrl { get; }

    /// <summary>
    /// base 64.
    /// </summary>
    public string? Base64 { get; }
}
