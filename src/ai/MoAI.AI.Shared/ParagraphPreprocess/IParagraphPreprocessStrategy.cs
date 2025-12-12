using MoAI.AI.Models;

namespace MoAI.AI.ParagraphPreprocess;

/// <summary>
/// 段落预处理策略接口
/// </summary>
public interface IParagraphPreprocessStrategy
{
    /// <summary>
    /// 执行预处理
    /// </summary>
    /// <param name="paragraph">原始段落</param>
    /// <param name="aiClient">AI 客户端（用于生成提纲/问题/摘要等）</param>
    /// <returns>预处理结果</returns>
    Task<ParagraphPreprocessResult> ProcessAsync(string paragraph, IParagraphPreprocessAiClient aiClient);

    /// <summary>
    /// 获取策略类型
    /// </summary>
    PreprocessStrategyType StrategyType { get; }
}