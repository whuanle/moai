using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.App.Classify.Queries;
using MoAI.App.Classify.Queries.Responses;
using MoAI.Database;

namespace MoAI.App.Classify.Queries;

/// <summary>
/// <inheritdoc cref="QueryAppClassifyListCommand"/>
/// </summary>
public class QueryAppClassifyListCommandHandler : IRequestHandler<QueryAppClassifyListCommand, QueryAppClassifyListCommandResponse>
{
    private readonly DatabaseContext _dbContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="QueryAppClassifyListCommandHandler"/> class.
    /// </summary>
    /// <param name="dbContext"></param>
    public QueryAppClassifyListCommandHandler(DatabaseContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <inheritdoc/>
    public async Task<QueryAppClassifyListCommandResponse> Handle(QueryAppClassifyListCommand request, CancellationToken cancellationToken)
    {
        // 获取用户所在的所有团队 ID
        var userTeamIds = await _dbContext.TeamUsers
            .Where(x => x.UserId == request.ContextUserId)
            .Select(x => x.TeamId)
            .ToListAsync(cancellationToken);

        // 查询所有应用分类及其对应的可访问应用数量
        var classifies = await _dbContext.Classifies
            .Where(x => x.Type == "app")
            .Select(x => new AppClassifyItem
            {
                ClassifyId = x.Id,
                Name = x.Name,
                Description = x.Description,
                AppCount = _dbContext.Apps
                    .Where(app =>
                    app.ClassifyId == x.Id &&
                    !app.IsDisable && !app.IsForeign &&
                    (app.IsPublic || userTeamIds.Contains(app.TeamId)))
                    .Count()
            })
            .ToArrayAsync(cancellationToken);

        return new QueryAppClassifyListCommandResponse
        {
            Items = classifies
        };
    }
}
