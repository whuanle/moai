using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.Database;
using MoAI.Database.Enums;
using MoAI.Wiki.External;
using MoAI.Wiki.Queries.Responses;

namespace MoAI.Wiki.Handlers;

/// <summary>
/// <inheritdoc cref="QueryExternalWikisCommand"/>
/// </summary>
public class QueryExternalWikisCommandHandler : IRequestHandler<QueryExternalWikisCommand, QueryWikisCommandResponse>
{
    private readonly DatabaseContext _databaseContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="QueryExternalWikisCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    public QueryExternalWikisCommandHandler(DatabaseContext databaseContext)
    {
        _databaseContext = databaseContext;
    }

    /// <inheritdoc/>
    public async Task<QueryWikisCommandResponse> Handle(QueryExternalWikisCommand request, CancellationToken cancellationToken)
    {
        // 外部接口按 token 归属团队过滤；应用 token 对团队知识库权限等价团队 Admin，MyRole 固定返回 Admin.
        var items = await _databaseContext.Wikis
            .Where(x => x.TeamId == request.Caller.TeamId && x.IsDeleted == 0)
            .OrderBy(x => x.Id)
            .Select(x => new WikiItem
            {
                WikiId = x.Id,
                TeamId = x.TeamId,
                Name = x.Name,
                Description = x.Description,
                IsPublic = x.IsPublic,
                AvatarPath = x.AvatarPath,
                CreateTime = x.CreateTime
            })
            .ToListAsync(cancellationToken);

        return new QueryWikisCommandResponse
        {
            TeamId = request.Caller.TeamId,
            MyRole = (int)TeamRole.Admin,
            Items = items
        };
    }
}
