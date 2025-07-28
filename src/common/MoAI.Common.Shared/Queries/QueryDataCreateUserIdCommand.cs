using MediatR;
using MoAI.Infra.Models;

namespace MoAI.Common.Queries;

public class QueryDataCreateUserIdCommand<TEntity> : IRequest<QueryDataCreateUserIdCommandResponse>
    where TEntity : AuditsInfo
{
    public int TEntityId { get; init; }
}

public class QueryDataCreateUserIdCommandResponse
{
    public int UserId { get; init; }
}