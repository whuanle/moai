using MoAI.AI.Models;
using MoAI.Infra.Extensions;
using MoAI.Infra.Models;
using System.Text.RegularExpressions;

namespace MoAI.AI.ParagraphPreprocess;

/// <summary>
/// 提纲生成策略
/// </summary>
public class OutlineGenerationStrategy : IParagraphPreprocessStrategy
{
    public PreprocessStrategyType StrategyType => PreprocessStrategyType.OutlineGeneration;

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

        // 构建生成提纲的提示词（适配中文场景）
        var prompt = $@"请为以下段落生成简洁的结构化提纲（控制在50字以内，突出核心信息）：
段落内容：{paragraph}
提纲要求：1. 仅保留核心观点和关键信息 2. 语言简洁 3. 符合中文表达习惯";

        var outline = await aiClient.GenerateTextAsync(prompt);

        // 清洗生成结果（去除多余换行、空格）
        outline = Regex.Replace(outline, @"\s+", " ").Trim();

        return new ParagraphPreprocessResult
        {
            OriginalText = paragraph,
            ProcessedText = outline,
            StrategyType = StrategyType,
            Metadata = new KeyValueString[]
            {
                new KeyValueString
                {
                    Key = ParagrahProcessorMetadataType.Outline.ToJsonString(),
                    Value = StrategyType.ToString()
                }
            }
        };
    }
}
