namespace MoAI.Account.Queries.Responses;

/// <summary>
/// 查询当前用户已绑定第三方账号的响应.
/// </summary>
public class QueryUserBoundAccountsCommandResponse
{
    /// <summary>
    /// 已绑定的第三方账号集合.
    /// </summary>
    public IReadOnlyCollection<BoundAccountInfo> Items { get; init; } = new List<BoundAccountInfo>();
}
