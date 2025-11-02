using MediatR;
using MoAI.Common.Queries.Response;
using MoAI.Infra.Models;

namespace MoAI.Common.Queries;

/// <summary>
/// 查询数据创建人.
/// </summary>
/// <typeparam name="TEntity">实体.</typeparam>
public class QueryDataCreateUserIdCommand<TEntity> : IRequest<QueryDataCreateUserIdCommandResponse>
    where TEntity : AuditsInfo
{
    /// <summary>
    /// 实体 id.
    /// </summary>
    public int TEntityId { get; init; }
}
