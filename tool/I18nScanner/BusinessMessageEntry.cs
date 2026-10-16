namespace I18nScanner;

/// <summary>
/// 单条 BusinessException 消息的提取结果.
/// </summary>
public sealed class BusinessMessageEntry
{
    /// <summary>
    /// 归一化后的消息模板；Unresolved 时为 null.
    /// </summary>
    public string? Template { get; init; }

    /// <summary>
    /// 消息表达式形态.
    /// </summary>
    public MessageExpressionKind Kind { get; init; }

    /// <summary>
    /// 源文件相对路径.
    /// </summary>
    public string File { get; init; } = string.Empty;

    /// <summary>
    /// 行号（1 起算）.
    /// </summary>
    public int Line { get; init; }

    /// <summary>
    /// 原始表达式源码.
    /// </summary>
    public string Expression { get; init; } = string.Empty;
}
