using MediatR;
using MoAI.Subsystem.Queries;
using MoAI.Subsystem.Queries.Responses;
using MoAI.Subsystem.Services;

namespace MoAI.Subsystem.Handlers;

/// <summary>
/// <inheritdoc cref="QuerySubsysEventsCommand"/>
/// </summary>
public class QuerySubsysEventsCommandHandler : IRequestHandler<QuerySubsysEventsCommand, QuerySubsysEventsCommandResponse>
{
    private readonly AccessEventStream _eventStream;

    /// <summary>
    /// Initializes a new instance of the <see cref="QuerySubsysEventsCommandHandler"/> class.
    /// </summary>
    /// <param name="eventStream">事件补发窗口.</param>
    public QuerySubsysEventsCommandHandler(AccessEventStream eventStream)
    {
        _eventStream = eventStream;
    }

    /// <inheritdoc/>
    public async Task<QuerySubsysEventsCommandResponse> Handle(QuerySubsysEventsCommand request, CancellationToken cancellationToken)
    {
        var result = await _eventStream.ReadAsync(request.SubsystemId, request.AfterId, request.Limit, cancellationToken);
        return new QuerySubsysEventsCommandResponse
        {
            Items = result.Items,
            HasMore = result.HasMore,
            ResyncRequired = result.ResyncRequired,
            OldestId = result.OldestId,
        };
    }
}
