namespace MoAI.Wiki.Models;

/// <summary>
/// 文档切片元数据生成策略类型.
/// </summary>
public enum MetadataGenerationStrategy
{
    /// <summary>
    /// 生成大纲.
    /// </summary>
    OutlineGeneration,

    /// <summary>
    /// 生成问题.
    /// </summary>
    QuestionGeneration,

    /// <summary>
    /// 生成关键词与摘要.
    /// </summary>
    KeywordSummaryFusion,

    /// <summary>
    /// 语义聚合.
    /// </summary>
    SemanticAggregation,
}