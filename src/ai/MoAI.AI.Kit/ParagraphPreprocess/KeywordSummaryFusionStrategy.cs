using MoAI.AI.Models;
using MoAI.Infra.Extensions;
using MoAI.Infra.Models;
using System.Text.RegularExpressions;

namespace MoAI.AI.ParagraphPreprocess;

/// <summary>
/// 关键词+摘要融合策略
/// </summary>
public class KeywordSummaryFusionStrategy : IParagraphPreprocessStrategy
{
    /// <summary>
    /// 提取的关键词数量
    /// </summary>
    public int KeywordCount { get; set; } = 5;

    public PreprocessStrategyType StrategyType => PreprocessStrategyType.KeywordSummaryFusion;

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

        // 生成关键词
        var keywordPrompt = $@"请提取以下段落的{KeywordCount}个核心关键词（用逗号分隔，仅保留关键词）：
段落内容：{paragraph}";
        var keywordsStr = await aiClient.GenerateTextAsync(keywordPrompt);
        var keywords = keywordsStr.Split(',')
                                  .Select(k => k.Trim())
                                  .Where(k => !string.IsNullOrWhiteSpace(k))
                                  .Take(KeywordCount)
                                  .ToList();

        // 生成精简摘要
        var summaryPrompt = $@"请为以下段落生成精简摘要（控制在80字以内）：
段落内容：{paragraph}";
        var summary = await aiClient.GenerateTextAsync(summaryPrompt);
        summary = Regex.Replace(summary, @"\s+", " ").Trim();

        // 融合关键词+摘要+核心内容（可根据需求调整拼接格式）
        var processedText = $"关键词：{string.Join(",", keywords)} | 摘要：{summary} | 核心内容：{GetMainContent(paragraph, 100)}";

        return new ParagraphPreprocessResult
        {
            OriginalText = paragraph,
            ProcessedText = processedText,
            StrategyType = StrategyType,
            Metadata = new KeyValueString[]
                {
                    new KeyValueString
                    {
                        Key = ParagrahProcessorMetadataType.Keyword.ToJsonString(),
                        Value = string.Join(separator: ",", keywords),
                    },
                    new KeyValueString
                    {
                        Key = ParagrahProcessorMetadataType.Summary.ToJsonString(),
                        Value = summary,
                    }
                }
        };
    }

    /// <summary>
    /// 提取段落核心内容（截断过长文本）
    /// </summary>
    private string GetMainContent(string text, int maxLength)
    {
        if (text.Length <= maxLength)
        {
            return text;
        }

        return text[..maxLength] + "...";
    }
}
