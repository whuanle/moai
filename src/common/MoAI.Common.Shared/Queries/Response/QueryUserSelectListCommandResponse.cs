namespace MoAI.Common.Queries.Response;

/// <summary>
/// 用户选择列表响应.
/// </summary>
public class QueryUserSelectListCommandResponse
{
    /// <summary>
    /// 用户列表.
    /// </summary>
    public List<UserSelectItem> Items { get; set; } = [];

    /// <summary>
    /// 总数.
    /// </summary>
    public int Total { get; set; }

    /// <summary>
    /// 页码.
    /// </summary>
    public int PageNo { get; set; }

    /// <summary>
    /// 每页大小.
    /// </summary>
    public int PageSize { get; set; }
}

/// <summary>
/// 用户选择项.
/// </summary>
public class UserSelectItem
{
    /// <summary>
    /// 用户ID.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// 用户名.
    /// </summary>
    public string UserName { get; set; } = default!;

    /// <summary>
    /// 昵称.
    /// </summary>
    public string NickName { get; set; } = default!;

    /// <summary>
    /// 头像路径.
    /// </summary>
    public string? AvatarPath { get; set; }
}
