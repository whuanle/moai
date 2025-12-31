using System.Text;
using System.Text.Json.Serialization;

namespace MoAI.AI.Models;

/// <summary>
/// 默认文件处理调用.
/// </summary>
public class DefaultAiProcessingFileCall : AiProcessingFileCall
{
    /// <inheritdoc/>
    public string? FileKey { get; set; } = string.Empty;

    /// <inheritdoc/>
    public string? FileUrl { get; set; } = string.Empty;

    /// <inheritdoc/>
    public string? Base64 { get; set; } = string.Empty;
}
