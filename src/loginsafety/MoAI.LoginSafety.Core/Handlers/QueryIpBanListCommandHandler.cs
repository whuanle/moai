using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.Database;
using MoAI.LoginSafety.Commands;
using MoAI.LoginSafety.Queries.Responses;

namespace MoAI.LoginSafety.Handlers;

/// <summary>
/// 分页查询 IP 封禁记录.
/// </summary>
public class QueryIpBanListCommandHandler : IRequestHandler<QueryIpBanListCommand, QueryIpBanListCommandResponse>
{
    private readonly DatabaseContext _databaseContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="QueryIpBanListCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    public QueryIpBanListCommandHandler(DatabaseContext databaseContext)
    {
        _databaseContext = databaseContext;
    }

    /// <inheritdoc/>
    public async Task<QueryIpBanListCommandResponse> Handle(QueryIpBanListCommand request, CancellationToken cancellationToken)
    {
        var query = _databaseContext.LoginIpBans.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(request.Ip))
        {
            query = query.Where(x => x.Ip == request.Ip);
        }

        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(x => x.CreateTime)
            .Skip(request.Skip)
            .Take(request.Take)
            .Select(x => new LoginIpBanItem
            {
                Id = x.Id,
                Ip = x.Ip,
                Reason = x.Reason,
                SourceRule = x.SourceRule,
                ExpireTime = x.ExpireTime,
                IsLifted = x.IsLifted,
                CreateTime = x.CreateTime
            })
            .ToListAsync(cancellationToken);

        return new QueryIpBanListCommandResponse
        {
            Items = items,
            Total = total,
            PageNo = request.PageNo,
            PageSize = request.PageSize
        };
    }
}
