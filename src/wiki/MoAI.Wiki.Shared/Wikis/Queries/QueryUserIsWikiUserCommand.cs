using MediatR;
using MoAI.Infra.Models;
using MoAI.Wiki.Wikis.Queries.Response;

namespace MoAI.Wiki.Wikis.Queries;

/// <summary>
/// 查询用户是否在知识库协作成员中.
/// </summary>
public class QueryUserIsWikiUserCommand : IUserIdContext, IRequest<QueryUserIsWikiUserCommandResponse>
{
    /// <summary>
    /// 知识库 id.
    /// </summary>
    public int WikiId { get; init; }

    /// <summary>
    /// 用户 id.
    /// </summary>
    public int ContextUserId { get; init; }
}
