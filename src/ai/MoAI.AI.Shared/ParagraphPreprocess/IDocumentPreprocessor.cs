using MoAI.AI.Models;

namespace MoAI.AI.ParagraphPreprocess;

/// <summary>
/// 文档处理策略.
/// </summary>
public interface IDocumentPreprocessor
{
    /// <summary>
    /// 批量对多个文本使用同一种策略生成.
    /// </summary>
    /// <typeparam name="TKey">key.</typeparam>
    /// <param name="paragraphs"></param>
    /// <param name="strategyType"></param>
    /// <returns></returns>
    Task<IReadOnlyDictionary<TKey, ParagraphPreprocessResult>> PreprocessBatchAsync<TKey>(IReadOnlyDictionary<TKey, string> paragraphs, PreprocessStrategyType strategyType)
        where TKey : notnull;

    /// <summary>
    /// 策略生成.
    /// </summary>
    /// <param name="paragraph"></param>
    /// <param name="strategyType"></param>
    /// <returns></returns>
    Task<ParagraphPreprocessResult> PreprocessParagraphAsync(string paragraph, PreprocessStrategyType strategyType);

    /// <summary>
    /// 对同一段落，使用不同策略生成.
    /// </summary>
    /// <param name="paragraph"></param>
    /// <param name="strategyTypes"></param>
    /// <returns></returns>
    Task<IReadOnlyCollection<ParagraphPreprocessResult>> PreprocessWithMultipleStrategiesAsync(string paragraph, IReadOnlyCollection<PreprocessStrategyType> strategyTypes);

    /// <summary>
    /// 注册自定义策略.
    /// </summary>
    /// <param name="strategy"></param>
    void RegisterStrategy(IParagraphPreprocessStrategy strategy);
}