using MediatR;
using MoAI.Wiki.Wikis.Queries.Response;

namespace MoAI.Wiki.Wikis.Queries;

/// <summary>
/// 查询用户是否在知识库协作成员中.
/// </summary>
public class QueryUserIsWikiUserCommand : IRequest<QueryUserIsWikiUserCommandResponse>
{
    /// <summary>
    /// 知识库 id.
    /// </summary>
    public int WikiId { get; init; }

    /// <summary>
    /// 用户 id.
    /// </summary>
    public int UserId { get; init; }
}
