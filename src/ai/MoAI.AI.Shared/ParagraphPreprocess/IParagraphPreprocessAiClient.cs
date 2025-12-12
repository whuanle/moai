namespace MoAI.AI.ParagraphPreprocess;

/// <summary>
/// AI 客户端接口（适配不同大模型）
/// </summary>
public interface IParagraphPreprocessAiClient
{
    /// <summary>
    /// 调用 AI 生成文本（如提纲、问题、摘要）
    /// </summary>
    /// <param name="prompt">提示词</param>
    /// <returns>生成的文本</returns>
    Task<string> GenerateTextAsync(string prompt);

    /// <summary>
    /// 计算两段文本的语义相似度（0-1）
    /// </summary>
    /// <param name="text1">文本1</param>
    /// <param name="text2">文本2</param>
    /// <returns>相似度得分</returns>
    Task<float> CalculateSimilarityAsync(string text1, string text2);
}
