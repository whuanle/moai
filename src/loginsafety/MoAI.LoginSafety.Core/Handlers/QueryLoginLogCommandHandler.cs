using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.Database;
using MoAI.LoginSafety.Commands;
using MoAI.LoginSafety.Queries.Responses;

namespace MoAI.LoginSafety.Handlers;

/// <summary>
/// 分页查询登录日志.
/// </summary>
public class QueryLoginLogCommandHandler : IRequestHandler<QueryLoginLogCommand, QueryLoginLogCommandResponse>
{
    private readonly DatabaseContext _databaseContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="QueryLoginLogCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    public QueryLoginLogCommandHandler(DatabaseContext databaseContext)
    {
        _databaseContext = databaseContext;
    }

    /// <inheritdoc/>
    public async Task<QueryLoginLogCommandResponse> Handle(QueryLoginLogCommand request, CancellationToken cancellationToken)
    {
        var query = _databaseContext.LoginLogs.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(request.UserName))
        {
            query = query.Where(x => x.UserName.Contains(request.UserName));
        }

        if (!string.IsNullOrWhiteSpace(request.Ip))
        {
            query = query.Where(x => x.Ip == request.Ip);
        }

        if (request.IsSuccess != null)
        {
            query = query.Where(x => x.IsSuccess == request.IsSuccess);
        }

        if (request.RiskLevel != null)
        {
            query = query.Where(x => x.RiskLevel == request.RiskLevel);
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
            .Select(x => new LoginLogItem
            {
                Id = x.Id,
                UserId = x.UserId,
                UserName = x.UserName,
                LoginType = x.LoginType,
                IsSuccess = x.IsSuccess,
                FailReason = x.FailReason,
                Ip = x.Ip,
                Country = x.Country,
                Province = x.Province,
                City = x.City,
                Isp = x.Isp,
                UserAgent = x.UserAgent,
                RiskLevel = x.RiskLevel,
                RiskReasons = x.RiskReasons,
                CreateTime = x.CreateTime
            })
            .ToListAsync(cancellationToken);

        return new QueryLoginLogCommandResponse
        {
            Items = items,
            Total = total,
            PageNo = request.PageNo,
            PageSize = request.PageSize
        };
    }
}
