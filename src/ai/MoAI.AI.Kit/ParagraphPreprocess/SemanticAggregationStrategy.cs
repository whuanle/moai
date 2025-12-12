using MoAI.AI.Models;
using MoAI.Infra.Extensions;
using MoAI.Infra.Models;
using System.Text.RegularExpressions;

namespace MoAI.AI.ParagraphPreprocess;

/// <summary>
/// 语义聚合策略（相似子段合并）
/// </summary>
public class SemanticAggregationStrategy : IParagraphPreprocessStrategy
{
    /// <summary>
    /// 相似度阈值（0-1），高于该值则合并
    /// </summary>
    public float SimilarityThreshold { get; set; } = 0.75f;

    /// <summary>
    /// 子段分割长度（按句子分割）
    /// </summary>
    public int SubParagraphMaxLength { get; set; } = 100;

    public PreprocessStrategyType StrategyType => PreprocessStrategyType.SemanticAggregation;

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

        // 1. 将长段落分割为小的子段（按句子/标点分割）
        var subParagraphs = SplitIntoSubParagraphs(paragraph);
        if (subParagraphs.Count <= 1)
        {
            // 只有一个子段，无需聚合
            return new ParagraphPreprocessResult
            {
                OriginalText = paragraph,
                ProcessedText = paragraph,
                StrategyType = StrategyType,
                Metadata = subParagraphs.Select(x=>new KeyValueString
                {
                    Key = ParagrahProcessorMetadataType.AggregatedSubParagraph.ToJsonString(),
                    Value = x
                }).ToArray()
            };
        }

        // 2. 语义聚类：合并相似子段
        var aggregatedGroups = new List<List<string>>();
        aggregatedGroups.Add(new List<string> { subParagraphs[0] });

        for (int i = 1; i < subParagraphs.Count; i++)
        {
            var currentSubPara = subParagraphs[i];
            var lastGroup = aggregatedGroups.Last();
            var lastSubPara = lastGroup.Last();

            // 计算当前子段与上一组最后一个子段的相似度
            var similarity = await aiClient.CalculateSimilarityAsync(currentSubPara, lastSubPara);

            if (similarity >= SimilarityThreshold)
            {
                // 相似度达标，加入当前组
                lastGroup.Add(currentSubPara);
            }
            else
            {
                // 新建分组
                aggregatedGroups.Add(new List<string> { currentSubPara });
            }
        }

        // 3. 合并每组子段为完整段落
        var aggregatedParagraphs = aggregatedGroups
            .Select(g => string.Join(" ", g))
            .ToList();

        var processedText = string.Join("\n", aggregatedParagraphs);

        return new ParagraphPreprocessResult
        {
            OriginalText = paragraph,
            ProcessedText = processedText,
            StrategyType = StrategyType,
            Metadata = aggregatedParagraphs.Select(x => new KeyValueString
            {
                Key = ParagrahProcessorMetadataType.AggregatedSubParagraph.ToJsonString(),
                Value = x
            }).ToArray()
        };
    }

    /// <summary>
    /// 将长段落分割为小的子段
    /// </summary>
    private List<string> SplitIntoSubParagraphs(string paragraph)
    {
        // 按中文标点分割句子（。！？；），兼顾英文标点
        var sentences = Regex.Split(paragraph, @"[。！？；.!?;]")
                             .Select(s => s.Trim())
                             .Where(s => !string.IsNullOrWhiteSpace(s))
                             .ToList();

        // 进一步分割超长句子
        var subParagraphs = new List<string>();
        foreach (var sentence in sentences)
        {
            if (sentence.Length <= SubParagraphMaxLength)
            {
                subParagraphs.Add(sentence);
            }
            else
            {
                // 按长度分割（保留完整语义，这里简化为按字符数分割）
                for (int i = 0; i < sentence.Length; i += SubParagraphMaxLength)
                {
                    var length = Math.Min(SubParagraphMaxLength, sentence.Length - i);
                    subParagraphs.Add(sentence.Substring(i, length));
                }
            }
        }

        return subParagraphs;
    }
}
