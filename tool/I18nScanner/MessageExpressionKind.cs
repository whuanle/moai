namespace I18nScanner;

/// <summary>
/// 消息提取形态.
/// </summary>
public enum MessageExpressionKind
{
    /// <summary>
    /// 纯字符串字面量，运行时 Message 与 key 完全一致.
    /// </summary>
    Literal,

    /// <summary>
    /// 插值字符串，已归一化为 {0}/{1} 模板；运行时 Message 是填充后的文本，需把抛出点改写为格式串 + Argments 才能命中翻译.
    /// </summary>
    Interpolated,

    /// <summary>
    /// 无法静态解析的表达式（变量、方法调用等）.
    /// </summary>
    Unresolved
}
