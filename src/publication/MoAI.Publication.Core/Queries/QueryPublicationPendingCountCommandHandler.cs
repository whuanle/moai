using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.Database;
using MoAI.Database.Enums;
using MoAI.Publication.Queries;

namespace MoAI.Publication.Queries;

/// <summary>
/// <inheritdoc cref="QueryPublicationPendingCountCommand"/>
/// </summary>
public class QueryPublicationPendingCountCommandHandler : IRequestHandler<QueryPublicationPendingCountCommand, QueryPublicationPendingCountCommandResponse>
{
    private readonly DatabaseContext _databaseContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="QueryPublicationPendingCountCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    public QueryPublicationPendingCountCommandHandler(DatabaseContext databaseContext)
    {
        _databaseContext = databaseContext;
    }

    /// <inheritdoc/>
    public async Task<QueryPublicationPendingCountCommandResponse> Handle(QueryPublicationPendingCountCommand request, CancellationToken cancellationToken)
    {
        var count = await _databaseContext.PublicationReviews.AsNoTracking()
            .CountAsync(x => x.State == (int)PublicationState.Pending, cancellationToken);

        return new QueryPublicationPendingCountCommandResponse { Count = count };
    }
}
