using MoAI.Infra.Models;

namespace MoAI.Wiki.Wikis.Queries.Response;

public class QueryWikiUsersCommandResponseItem : AuditsInfo
{
    /// <summary>
    /// 用户ID.
    /// </summary>
    public int Id { get; init; }

    /// <summary>
    /// 用户名.
    /// </summary>
    public string UserName { get; init; } = default!;

    /// <summary>
    /// 邮箱.
    /// </summary>
    public string Email { get; init; } = default!;

    /// <summary>
    /// 昵称.
    /// </summary>
    public string NickName { get; init; } = default!;
}
