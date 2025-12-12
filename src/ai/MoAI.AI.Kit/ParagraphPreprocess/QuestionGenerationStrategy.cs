using MoAI.AI.Models;
using MoAI.Infra.Extensions;
using MoAI.Infra.Models;
using System.Text.RegularExpressions;

namespace MoAI.AI.ParagraphPreprocess;

/// <summary>
/// 提问生成策略
/// </summary>
public class QuestionGenerationStrategy : IParagraphPreprocessStrategy
{
    /// <summary>
    /// 每个段落生成的问题数量
    /// </summary>
    public int QuestionCount { get; set; } = 2;

    public PreprocessStrategyType StrategyType => PreprocessStrategyType.QuestionGeneration;

    /// <inheritdoc/>
    public async Task<ParagraphPreprocessResult> ProcessAsync(string paragraph, IParagraphPreprocessAiClient aiClient)
    {
        if (string.IsNullOrWhiteSpace(paragraph))
        {
            return new ParagraphPreprocessResult
            {
                OriginalText = paragraph,
                ProcessedText = string.Empty,
                StrategyType = StrategyType
            };
        }

        // 构建生成问题的提示词
        var prompt = $@"请基于以下段落生成{QuestionCount}个核心问题（仅保留问题，每行一个，控制在30字以内）：
段落内容：{paragraph}
问题要求：1. 覆盖段落核心信息 2. 符合用户实际提问习惯 3. 中文表述";

        var questionsStr = await aiClient.GenerateTextAsync(prompt);
        var questions = Regex.Split(questionsStr, @"\r?\n")
                             .Select(q => q.Trim())
                             .Where(q => !string.IsNullOrWhiteSpace(q))
                             .Take(QuestionCount)
                             .ToList();

        // 拼接问题作为向量化文本（也可选择单个问题分别向量化）
        var processedText = string.Join(" | ", questions);

        return new ParagraphPreprocessResult
        {
            OriginalText = paragraph,
            ProcessedText = processedText,
            StrategyType = StrategyType,
            Metadata = questions.Select(x => new KeyValueString
            {
                Key = ParagrahProcessorMetadataType.Question.ToJsonString(),
                Value = x
            }).ToArray()
        };
    }
}
