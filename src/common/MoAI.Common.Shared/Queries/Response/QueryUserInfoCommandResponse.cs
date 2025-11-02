namespace MoAI.Common.Queries.Response;

/// <summary>
/// 用户基本信息响应.
/// </summary>
public class QueryUserInfoCommandResponse
{
    /// <summary>
    /// 用户 ID.
    /// </summary>
    public int UserId { get; init; }

    /// <summary>
    /// 用户名.
    /// </summary>
    public string UserName { get; init; } = default!;

    /// <summary>
    /// 昵称.
    /// </summary>
    public string NickName { get; init; } = default!;

    /// <summary>
    /// 头像路径.
    /// </summary>
    public string Avatar { get; init; } = default!;

    /// <summary>
    /// 是否超级管理员.
    /// </summary>
    public bool IsRoot { get; init; } = default!;

    /// <summary>
    /// 是否管理员.
    /// </summary>
    public bool IsAdmin { get; init; } = default!;
}
