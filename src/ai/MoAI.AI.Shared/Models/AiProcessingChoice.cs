using MoAI.Wiki.Models;
using System.Text.Json.Serialization;

namespace MoAI.AI.Models;

/// <summary>
/// 执行信息.
/// </summary>
public class AiProcessingChoice
{
    /// <summary>
    /// 方便前端聚合数据.
    /// </summary>
    public Guid Id { get; init; } = Guid.CreateVersion7();

    /// <summary>
    /// 流类型.
    /// </summary>
    public AiProcessingChatStreamType StreamType { get; init; }

    /// <summary>
    /// 流状态.
    /// </summary>
    public AiProcessingChatStreamState StreamState { get; init; }

    /// <summary>
    /// 插件调用.
    /// </summary>
    public AiProcessingPluginCall? PluginCall { get; init; }

    /// <summary>
    /// 文本执行结果.
    /// </summary>
    public AiProcessingTextCall? TextCall { get; init; }

    /// <summary>
    /// 文件.
    /// </summary>
    public AiProcessingFileCall? FileCall { get; set; }
}