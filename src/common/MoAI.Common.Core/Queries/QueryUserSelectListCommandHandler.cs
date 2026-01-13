using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.Common.Queries.Response;
using MoAI.Database;

namespace MoAI.Common.Queries;

/// <summary>
/// <inheritdoc cref="QueryUserSelectListCommand"/>
/// </summary>
public class QueryUserSelectListCommandHandler : IRequestHandler<QueryUserSelectListCommand, QueryUserSelectListCommandResponse>
{
    private readonly DatabaseContext _databaseContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="QueryUserSelectListCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    public QueryUserSelectListCommandHandler(DatabaseContext databaseContext)
    {
        _databaseContext = databaseContext;
    }

    /// <inheritdoc/>
    public async Task<QueryUserSelectListCommandResponse> Handle(QueryUserSelectListCommand request, CancellationToken cancellationToken)
    {
        var query = _databaseContext.Users
            .Where(x => !x.IsDisable)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            query = query.Where(x => x.UserName.Contains(request.Search) || x.NickName.Contains(request.Search));
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var users = await query
            .OrderBy(x => x.NickName)
            .Skip(request.Skip)
            .Take(request.Take)
            .Select(x => new UserSelectItem
            {
                Id = x.Id,
                UserName = x.UserName,
                NickName = x.NickName,
                AvatarPath = x.AvatarPath
            })
            .ToListAsync(cancellationToken);

        return new QueryUserSelectListCommandResponse
        {
            Items = users,
            Total = totalCount,
            PageNo = request.PageNo,
            PageSize = request.PageSize
        };
    }
}
