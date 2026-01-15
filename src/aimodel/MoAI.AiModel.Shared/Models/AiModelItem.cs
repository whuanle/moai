namespace MoAI.AiModel.Models;

/// <summary>
/// 模型项.
/// </summary>
public class AiModelItem : AiNotKeyEndpoint
{
    /// <summary>
    /// 公开给用户使用.
    /// </summary>
    public bool IsPublic { get; init; }

    /// <summary>
    /// 使用量计数.
    /// </summary>
    public int Counter { get; init; }
}