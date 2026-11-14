using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.Database;
using MoAI.LoginSafety.Commands;
using MoAI.LoginSafety.Queries.Responses;

namespace MoAI.LoginSafety.Handlers;

/// <summary>
/// 分页查询登录风险事件.
/// </summary>
public class QueryRiskEventListCommandHandler : IRequestHandler<QueryRiskEventListCommand, QueryRiskEventListCommandResponse>
{
    private readonly DatabaseContext _databaseContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="QueryRiskEventListCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    public QueryRiskEventListCommandHandler(DatabaseContext databaseContext)
    {
        _databaseContext = databaseContext;
    }

    /// <inheritdoc/>
    public async Task<QueryRiskEventListCommandResponse> Handle(QueryRiskEventListCommand request, CancellationToken cancellationToken)
    {
        var query = _databaseContext.LoginRiskEvents.AsNoTracking().AsQueryable();

        if (request.IsRead != null)
        {
            query = query.Where(x => x.IsRead == request.IsRead);
        }

        if (request.RiskLevel != null)
        {
            query = query.Where(x => x.RiskLevel == request.RiskLevel);
        }

        if (!string.IsNullOrWhiteSpace(request.UserName))
        {
            query = query.Where(x => x.UserName.Contains(request.UserName));
        }

        if (request.StartTime != null)
        {
            query = query.Where(x => x.CreateTime >= request.StartTime);
        }

        if (request.EndTime != null)
        {
            query = query.Where(x => x.CreateTime <= request.EndTime);
        }

        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(x => x.CreateTime)
            .Skip(request.Skip)
            .Take(request.Take)
            .Select(x => new LoginRiskEventItem
            {
                Id = x.Id,
                UserId = x.UserId,
                UserName = x.UserName,
                RuleCode = x.RuleCode,
                RiskLevel = x.RiskLevel,
                Summary = x.Summary,
                Detail = x.Detail,
                IsRead = x.IsRead,
                Ip = x.Ip,
                Province = x.Province,
                City = x.City,
                CreateTime = x.CreateTime
            })
            .ToListAsync(cancellationToken);

        return new QueryRiskEventListCommandResponse
        {
            Items = items,
            Total = total,
            PageNo = request.PageNo,
            PageSize = request.PageSize
        };
    }
}
