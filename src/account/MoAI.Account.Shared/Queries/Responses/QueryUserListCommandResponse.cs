using MoAI.Account.Queries.Responses;

namespace MoAI.Account.Queries.Responses;

/// <summary>
/// 用户列表查询结果.
/// </summary>
public class QueryUserListCommandResponse
{
    /// <summary>
    /// 总数量.
    /// </summary>
    public int TotalCount { get; init; }

    /// <summary>
    /// 用户列表.
    /// </summary>
    public IReadOnlyList<QueryUserListCommandResponseItem> Items { get; init; } = Array.Empty<QueryUserListCommandResponseItem>();
}
