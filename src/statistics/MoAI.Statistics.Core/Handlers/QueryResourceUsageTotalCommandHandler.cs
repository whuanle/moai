using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.Database;
using MoAI.Statistics.Queries;
using MoAI.Statistics.Queries.Responses;

namespace MoAI.Statistics.Handlers;

/// <summary>
/// <inheritdoc cref="QueryResourceUsageTotalCommand"/>
/// </summary>
public class QueryResourceUsageTotalCommandHandler : IRequestHandler<QueryResourceUsageTotalCommand, QueryResourceUsageTotalCommandResponse>
{
    private readonly DatabaseContext _databaseContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="QueryResourceUsageTotalCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    public QueryResourceUsageTotalCommandHandler(DatabaseContext databaseContext)
    {
        _databaseContext = databaseContext;
    }

    /// <inheritdoc/>
    public async Task<QueryResourceUsageTotalCommandResponse> Handle(QueryResourceUsageTotalCommand request, CancellationToken cancellationToken)
    {
        var query = _databaseContext.ResourceUsageTotals
            .AsNoTracking()
            .AsQueryable();

        if (!string.IsNullOrEmpty(request.Name))
        {
            query = query.Where(x => x.Name == request.Name);
        }

        var total = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(x => x.Total)
            .ThenBy(x => x.Name)
            .ThenBy(x => x.ResourceId)
            .Skip((request.PageNo - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(x => new ResourceUsageTotalItem
            {
                Id = x.Id,
                Name = x.Name,
                ResourceId = x.ResourceId,
                Total = x.Total,
                UpdateTime = x.UpdateTime,
            })
            .ToListAsync(cancellationToken);

        return new QueryResourceUsageTotalCommandResponse
        {
            Items = items,
            Total = total,
            PageNo = request.PageNo,
            PageSize = request.PageSize,
        };
    }
}
