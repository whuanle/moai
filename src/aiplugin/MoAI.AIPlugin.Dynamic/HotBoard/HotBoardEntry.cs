namespace MoAI.AIPlugin.Dynamic.HotBoard;

/// <summary>
/// 热榜渠道归一化条目（渠道内产物，排名与来源信息由插件合并时补齐）.
/// </summary>
public sealed record HotBoardEntry
{
    /// <summary>
    /// 标题.
    /// </summary>
    public required string Title { get; init; }

    /// <summary>
    /// 摘要或补充信息（可空）.
    /// </summary>
    public string? Desc { get; init; }

    /// <summary>
    /// 原文链接（可空）.
    /// </summary>
    public string? Url { get; init; }

    /// <summary>
    /// 封面图（可空）.
    /// </summary>
    public string? Cover { get; init; }

    /// <summary>
    /// 热度值（可空，随平台口径不同）.
    /// </summary>
    public long? HotValue { get; init; }
}
