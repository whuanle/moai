namespace MoAI.Memory.Queries.Responses;

/// <summary>
/// 应用记忆统计响应.
/// </summary>
public class QueryAppMemoryStatsResponse
{
    /// <summary>
    /// 应用内全部用户的记忆总条数.
    /// </summary>
    public int TotalMemories { get; init; }

    /// <summary>
    /// 已有记忆的用户数.
    /// </summary>
    public int UserCount { get; init; }
}
