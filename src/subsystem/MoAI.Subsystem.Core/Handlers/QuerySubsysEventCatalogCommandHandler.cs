using Mediator;
using MoAI.Subsystem.Models;
using MoAI.Subsystem.Queries;
using MoAI.Subsystem.Queries.Responses;

namespace MoAI.Subsystem.Handlers;

/// <summary>
/// <inheritdoc cref="QuerySubsysEventCatalogCommand"/>
/// </summary>
public class QuerySubsysEventCatalogCommandHandler : IRequestHandler<QuerySubsysEventCatalogCommand, QuerySubsysEventCatalogCommandResponse>
{
    /// <inheritdoc/>
    public ValueTask<QuerySubsysEventCatalogCommandResponse> Handle(QuerySubsysEventCatalogCommand request, CancellationToken cancellationToken)
    {
        return ValueTask.FromResult(new QuerySubsysEventCatalogCommandResponse
        {
            SchemaVersion = SubsystemEventContract.SchemaVersion,
            RetentionDays = SubsystemEventContract.RetentionDays,
            Items = SubsystemEventCatalog.All,
        });
    }
}
