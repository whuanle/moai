namespace MoAI.AI.Models;

/// <summary>
/// 文档段落预处理策略类型
/// </summary>
public enum PreprocessStrategyType
{
    /// <summary>
    /// 段落提纲生成
    /// </summary>
    OutlineGeneration,

    /// <summary>
    /// 段落提问生成
    /// </summary>
    QuestionGeneration,

    /// <summary>
    /// 关键词+摘要融合
    /// </summary>
    KeywordSummaryFusion,

    /// <summary>
    /// 语义聚合（相似子段合并）
    /// </summary>
    SemanticAggregation
}
