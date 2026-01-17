namespace MoAI.App.Classify.Queries.Responses;

/// <summary>
/// 查询应用分类列表响应.
/// </summary>
public class QueryAppClassifyListCommandResponse
{
    /// <summary>
    /// 子项.
    /// </summary>
    public IReadOnlyCollection<AppClassifyItem> Items { get; init; } = Array.Empty<AppClassifyItem>();
}
