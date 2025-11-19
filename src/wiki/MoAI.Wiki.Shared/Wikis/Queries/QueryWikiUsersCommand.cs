using MediatR;
using MoAI.Wiki.Wikis.Queries.Response;

namespace MoAI.Wiki.Wikis.Queries;

/// <summary>
/// 查询知识库协作成员.
/// </summary>
public class QueryWikiUsersCommand : IRequest<QueryWikiUsersCommandResponse>
{
    /// <summary>
    /// 查询知识库协作的成员列表.
    /// </summary>
    public int WikiId { get; init; }
}
