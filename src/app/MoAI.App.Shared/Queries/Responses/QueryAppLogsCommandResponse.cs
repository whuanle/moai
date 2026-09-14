namespace MoAI.App.Queries.Responses;

/// <summary>
/// 应用对话日志分页结果.
/// </summary>
public class QueryAppLogsCommandResponse
{
    /// <summary>日志条目.</summary>
    public IReadOnlyList<AppLogItem> Items { get; set; } = new List<AppLogItem>();

    /// <summary>总数量.</summary>
    public int Total { get; set; }

    /// <summary>页码.</summary>
    public int PageNo { get; set; }

    /// <summary>每页数量.</summary>
    public int PageSize { get; set; }
}
