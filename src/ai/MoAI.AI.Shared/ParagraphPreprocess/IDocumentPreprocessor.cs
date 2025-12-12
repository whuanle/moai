using MoAI.AI.Models;

namespace MoAI.AI.ParagraphPreprocess
{
    public interface IDocumentPreprocessor
    {
        Task<List<ParagraphPreprocessResult>> PreprocessBatchAsync(IReadOnlyCollection<string> paragraphs, PreprocessStrategyType strategyType);
        Task<ParagraphPreprocessResult> PreprocessParagraphAsync(string paragraph, PreprocessStrategyType strategyType);
        Task<List<ParagraphPreprocessResult>> PreprocessWithMultipleStrategiesAsync(string paragraph, IReadOnlyCollection<PreprocessStrategyType> strategyTypes);
        void RegisterStrategy(IParagraphPreprocessStrategy strategy);
    }
}