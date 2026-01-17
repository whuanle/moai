namespace MoAI.App.Classify.Queries.Responses;

/// <summary>
/// 应用分类.
/// </summary>
public class AppClassifyItem
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

    /// <summary>
    /// 当前用户可以使用的应用数量.
    /// </summary>
    public int AppCount { get; init; }
}
