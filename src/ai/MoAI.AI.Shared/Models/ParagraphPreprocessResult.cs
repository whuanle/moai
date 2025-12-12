using MoAI.Infra.Models;

namespace MoAI.AI.Models;

/// <summary>
/// 段落预处理结果
/// </summary>
public class ParagraphPreprocessResult
{
    /// <summary>
    /// 原始段落文本
    /// </summary>
    public string OriginalText { get; set; } = string.Empty;

    /// <summary>
    /// 预处理后的文本（用于向量化）
    /// </summary>
    public string ProcessedText { get; set; } = string.Empty;

    /// <summary>
    /// 使用的策略类型
    /// </summary>
    public PreprocessStrategyType StrategyType { get; set; }

    /// <summary>
    /// 附加元数据（如关键词、摘要、问题列表等）
    /// </summary>
    public IReadOnlyCollection<KeyValueString> Metadata { get; set; } = Array.Empty<KeyValueString>();
}
