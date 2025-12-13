using MoAI.AI.Models;
using MoAI.Infra.Models;

namespace MoAI.AI.ParagraphPreprocess;

/// <summary>
/// 文档预处理工具类（核心入口）
/// </summary>
public class DocumentPreprocessor : IDocumentPreprocessor
{
    private readonly Dictionary<PreprocessStrategyType, IParagraphPreprocessStrategy> _strategies = new();
    private readonly IParagraphPreprocessAiClient _aiClient;

    /// <summary>
    /// Initializes a new instance of the <see cref="DocumentPreprocessor"/> class.
    /// </summary>
    /// <param name="aiClient"></param>
    public DocumentPreprocessor(IParagraphPreprocessAiClient aiClient)
    {
        _aiClient = aiClient;

        // 注册默认策略
        RegisterStrategy(new OutlineGenerationStrategy());
        RegisterStrategy(new QuestionGenerationStrategy());
        RegisterStrategy(new KeywordSummaryFusionStrategy());
        RegisterStrategy(new SemanticAggregationStrategy());
    }

    /// <inheritdoc/>
    public void RegisterStrategy(IParagraphPreprocessStrategy strategy)
    {
        _strategies[strategy.StrategyType] = strategy;
    }

    /// <inheritdoc/>
    public async Task<ParagraphPreprocessResult> PreprocessParagraphAsync(
        string paragraph,
        PreprocessStrategyType strategyType)
    {
        if (!_strategies.TryGetValue(strategyType, out var strategy))
        {
            throw new ArgumentException($"预处理策略 {strategyType} 未注册", nameof(strategyType));
        }

        return await strategy.ProcessAsync(paragraph, _aiClient);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyCollection<ParagraphPreprocessResult>> PreprocessWithMultipleStrategiesAsync(
        string paragraph,
        IReadOnlyCollection<PreprocessStrategyType> strategyTypes)
    {
        var indexedStrategies = strategyTypes.Select((type, index) => (type, index)).ToList();
        var tasks = indexedStrategies.Select(async item =>
        {
            var result = await PreprocessParagraphAsync(paragraph, item.type);
            return (item.index, result);
        });

        var results = await Task.WhenAll(tasks);

        return results
            .OrderBy(r => r.index)
            .Select(r => r.result)
            .ToList();
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyDictionary<TKey, ParagraphPreprocessResult>> PreprocessBatchAsync<TKey>(IReadOnlyDictionary<TKey, string> paragraphs, PreprocessStrategyType strategyType)
        where TKey : notnull
    {
        var tasks = paragraphs.Select(async item =>
        {
            var result = await PreprocessParagraphAsync(item.Value, strategyType);
            return new KeyValuePair<TKey, ParagraphPreprocessResult>(item.Key, result);
        });

        var results = await Task.WhenAll(tasks);

        return results.ToDictionary();
    }
}