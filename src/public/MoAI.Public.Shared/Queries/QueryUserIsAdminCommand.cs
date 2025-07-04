using MediatR;
using MoAI.Public.Queries.Response;

namespace MoAI.Public.Queries;

public class QueryUserIsAdminCommand : IRequest<QueryUserIsAdminCommandResponse>
{
    public int UserId { get; init; }
}
