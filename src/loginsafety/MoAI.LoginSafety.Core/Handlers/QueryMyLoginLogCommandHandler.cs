using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.Database;
using MoAI.LoginSafety.Commands;
using MoAI.LoginSafety.Queries.Responses;

namespace MoAI.LoginSafety.Handlers;

/// <summary>
/// 查询当前用户本人的登录记录（仅成功登录）.
/// </summary>
public class QueryMyLoginLogCommandHandler : IRequestHandler<QueryMyLoginLogCommand, QueryMyLoginLogCommandResponse>
{
    private readonly DatabaseContext _databaseContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="QueryMyLoginLogCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    public QueryMyLoginLogCommandHandler(DatabaseContext databaseContext)
    {
        _databaseContext = databaseContext;
    }

    /// <inheritdoc/>
    public async Task<QueryMyLoginLogCommandResponse> Handle(QueryMyLoginLogCommand request, CancellationToken cancellationToken)
    {
        var query = _databaseContext.LoginLogs.AsNoTracking()
            .Where(x => x.UserId == request.ContextUserId && x.IsSuccess);

        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(x => x.CreateTime)
            .Skip(request.Skip)
            .Take(request.Take)
            .Select(x => new MyLoginLogItem
            {
                Id = x.Id,
                LoginType = x.LoginType,
                Ip = x.Ip,
                Province = x.Province,
                City = x.City,
                CreateTime = x.CreateTime
            })
            .ToListAsync(cancellationToken);

        return new QueryMyLoginLogCommandResponse
        {
            Items = items,
            Total = total,
            PageNo = request.PageNo,
            PageSize = request.PageSize
        };
    }
}
