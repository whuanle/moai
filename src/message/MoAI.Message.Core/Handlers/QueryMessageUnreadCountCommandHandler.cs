using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.Database;
using MoAI.Infra.Models;
using MoAI.Message.Queries;

namespace MoAI.Message.Handlers;

/// <summary>
/// <inheritdoc cref="QueryMessageUnreadCountCommand"/>.
/// </summary>
public class QueryMessageUnreadCountCommandHandler : IRequestHandler<QueryMessageUnreadCountCommand, SimpleLong>
{
    private readonly DatabaseContext _databaseContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="QueryMessageUnreadCountCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    public QueryMessageUnreadCountCommandHandler(DatabaseContext databaseContext)
    {
        _databaseContext = databaseContext;
    }

    /// <inheritdoc/>
    public async Task<SimpleLong> Handle(QueryMessageUnreadCountCommand request, CancellationToken cancellationToken)
    {
        var count = await _databaseContext.Messages
            .AsNoTracking()
            .Where(x => x.RecipientUserId == request.ContextUserId && !x.IsRead)
            .CountAsync(cancellationToken);

        return new SimpleLong { Value = count };
    }
}
