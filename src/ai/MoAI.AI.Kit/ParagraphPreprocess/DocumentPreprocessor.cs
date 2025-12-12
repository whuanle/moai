using MoAI.AI.Models;

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

    /// <summary>
    /// 注册自定义预处理策略
    /// </summary>
    /// <param name="strategy">策略实例</param>
    public void RegisterStrategy(IParagraphPreprocessStrategy strategy)
    {
        _strategies[strategy.StrategyType] = strategy;
    }

    /// <summary>
    /// 预处理单个段落
    /// </summary>
    /// <param name="paragraph">原始段落文本</param>
    /// <param name="strategyType">策略类型</param>
    /// <returns>预处理结果</returns>
    /// <exception cref="ArgumentException">策略未注册时抛出</exception>
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

    /// <summary>
    /// 批量预处理文档段落
    /// </summary>
    /// <param name="paragraphs">段落列表</param>
    /// <param name="strategyType">策略类型</param>
    /// <returns>批量预处理结果</returns>
    public async Task<List<ParagraphPreprocessResult>> PreprocessBatchAsync(
        IReadOnlyCollection<string> paragraphs,
        PreprocessStrategyType strategyType)
    {
        var indexedParagraphs = paragraphs.Select((paragraph, index) => (paragraph, index)).ToList();
        var tasks = indexedParagraphs.Select(async item =>
        {
            var result = await PreprocessParagraphAsync(item.paragraph, strategyType);
            return (item.index, result);
        });

        var results = await Task.WhenAll(tasks);

        return results
            .OrderBy(r => r.index)
            .Select(r => r.result)
            .ToList();
    }

    /// <summary>
    /// 多策略组合预处理（生成多个版本的向量化文本）
    /// </summary>
    /// <param name="paragraph">原始段落</param>
    /// <param name="strategyTypes">策略类型列表</param>
    /// <returns>多策略预处理结果</returns>
    public async Task<List<ParagraphPreprocessResult>> PreprocessWithMultipleStrategiesAsync(
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
}