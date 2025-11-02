namespace MoAI.Prompt.PromptEndpoints.Models;

public class AiOptimizePromptRequest
{
    /// <summary>
    /// AI 模型 id.
    /// </summary>
    public int AiModelId { get; init; }

    /// <summary>
    /// 用户原本的提示词
    /// </summary>
    public required string SourcePrompt { get; init; }
}
