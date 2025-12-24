using MoAI.Wiki.Models;
using System.Text.Json.Serialization;

namespace MoAI.AI.Models;

/// <summary>
/// 知识库调用.
/// </summary>
public abstract class AiProcessingWikiCall
{
    /// <summary>
    /// 知识库id.
    /// </summary>
    public int WikiId { get; protected set; }

    /// <summary>
    /// 插件执行状态.
    /// </summary>
    public WorkerState WorkerState { get; protected set; }

    /// <summary>
    /// 信息，如果报错，会有错误信息.
    /// </summary>
    public string? Message { get; protected set; }

    /// <summary>
    /// 提问.
    /// </summary>
    public string Question { get; protected set; } = string.Empty;

    /// <summary>
    /// 知识库搜索结果.
    /// </summary>
    public string Result { get; protected set; } = string.Empty;
}

/// <summary>
/// 知识库调用.
/// </summary>
public class DefaultAiProcessingWikiCall : AiProcessingWikiCall
{
}
