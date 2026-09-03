using System.Collections.Generic;

namespace MoAI.Classify.Queries.Responses;

/// <summary>
/// 分类列表查询响应.
/// </summary>
public class QueryClassifyListCommandResponse
{
    /// <summary>
    /// 分类列表.
    /// </summary>
    public List<ClassifyItem> Items { get; init; } = new();
}
