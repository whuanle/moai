namespace MoAI.Common.Queries.Response;

/// <summary>
/// 用户是否管理员.
/// </summary>
public class QueryUserIsAdminCommandResponse
{
    /// <summary>
    /// 管理员.
    /// </summary>
    public bool IsAdmin { get; init; }

    /// <summary>
    /// 超级管理员.
    /// </summary>
    public bool IsRoot { get; init; }
}
