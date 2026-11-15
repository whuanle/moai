using MediatR;
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
    public Task<QuerySubsysEventCatalogCommandResponse> Handle(QuerySubsysEventCatalogCommand request, CancellationToken cancellationToken)
    {
        return Task.FromResult(new QuerySubsysEventCatalogCommandResponse
        {
            SchemaVersion = SubsystemEventContract.SchemaVersion,
            RetentionDays = SubsystemEventContract.RetentionDays,
            Items = SubsystemEventCatalog.All,
        });
    }
}
