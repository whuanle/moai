namespace MoAI.Prompt.Queries.Responses;

public class PromptClassifyItem
{
    /// <summary>
    /// Id.
    /// </summary>
    public int ClassifyId { get; init; }

    /// <summary>
    /// 名字.
    /// </summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// 描述.
    /// </summary>
    public string Description { get; init; } = string.Empty;
}