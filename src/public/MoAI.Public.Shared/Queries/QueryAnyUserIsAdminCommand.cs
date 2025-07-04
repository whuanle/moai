using MediatR;
using MoAI.Infra.Models;

namespace MoAI.Login.Queries;

public class QueryAnyUserIsAdminCommand : IRequest<SimpleBool>
{
    public IReadOnlyCollection<int> UserIds { get; init; } = Array.Empty<int>();
}