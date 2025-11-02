using MediatR;
using MoAI.Database;
using MoAI.Infra.Models;
using MoAI.Login.Commands;

namespace MoAI.Login.Queries;

/// <summary>
/// <inheritdoc cref="QueryAnyUserIsAdminCommand"/>
/// </summary>
public class QueryAnyUserIsAdminCommandHandler : IRequestHandler<QueryAnyUserIsAdminCommand, SimpleBool>
{
    private readonly DatabaseContext _databaseContext;
    private readonly IMediator _mediator;

    /// <summary>
    /// Initializes a new instance of the <see cref="QueryAnyUserIsAdminCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext"></param>
    /// <param name="mediator"></param>
    public QueryAnyUserIsAdminCommandHandler(DatabaseContext databaseContext, IMediator mediator)
    {
        _databaseContext = databaseContext;
        _mediator = mediator;
    }

    /// <inheritdoc/>
    public async Task<SimpleBool> Handle(QueryAnyUserIsAdminCommand request, CancellationToken cancellationToken)
    {
        if (request.UserIds.Count == 0)
        {
            return new SimpleBool { Value = false };
        }

        var adminList = await _mediator.Send(new QueryAdminIdsCommand(), cancellationToken);

        if (request.UserIds.Contains(adminList.RootId))
        {
            return new SimpleBool { Value = true };
        }

        HashSet<int> set1 = [.. request.UserIds];
        HashSet<int> set2 = [.. adminList.AdminIds];

        bool hasCommonElements = set1.Overlaps(set2);

        return new SimpleBool
        {
            Value = hasCommonElements
        };
    }
}