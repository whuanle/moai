using System;
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
/// <inheritdoc cref="QueryResourceUsageDailyCommand"/>
/// </summary>
public class QueryResourceUsageDailyCommandHandler : IRequestHandler<QueryResourceUsageDailyCommand, QueryResourceUsageDailyCommandResponse>
{
    private readonly DatabaseContext _databaseContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="QueryResourceUsageDailyCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    public QueryResourceUsageDailyCommandHandler(DatabaseContext databaseContext)
    {
        _databaseContext = databaseContext;
    }

    /// <inheritdoc/>
    public async Task<QueryResourceUsageDailyCommandResponse> Handle(QueryResourceUsageDailyCommand request, CancellationToken cancellationToken)
    {
        var query = _databaseContext.ResourceUsageDailies
            .AsNoTracking()
            .AsQueryable();

        if (!string.IsNullOrEmpty(request.Name))
        {
            query = query.Where(x => x.Name == request.Name);
        }

        if (request.StartDate != null)
        {
            query = query.Where(x => x.UsageDate >= request.StartDate);
        }

        if (request.EndDate != null)
        {
            query = query.Where(x => x.UsageDate <= request.EndDate);
        }

        var total = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(x => x.UsageDate)
            .ThenByDescending(x => x.UsageHour)
            .ThenBy(x => x.Name)
            .ThenBy(x => x.ResourceId)
            .Skip((request.PageNo - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(x => new ResourceUsageDailyItem
            {
                Id = x.Id,
                UsageDate = x.UsageDate,
                UsageHour = x.UsageHour,
                Name = x.Name,
                ResourceId = x.ResourceId,
                Count = x.Count,
                UpdateTime = x.UpdateTime,
            })
            .ToListAsync(cancellationToken);

        return new QueryResourceUsageDailyCommandResponse
        {
            Items = items,
            Total = total,
            PageNo = request.PageNo,
            PageSize = request.PageSize,
        };
    }
}
