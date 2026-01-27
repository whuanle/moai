using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.App.Chat.Chats.Models;
using MoAI.App.Models;
using MoAI.Database;
using MoAI.Storage.Queries;

namespace MoAI.App.AppStore.Queries;

/// <summary>
/// <inheritdoc cref="QueryAccessibleAppListCommand"/>
/// </summary>
public class QueryAccessibleAppListCommandHandler : IRequestHandler<QueryAccessibleAppListCommand, QueryAccessibleAppListCommandResponse>
{
    private readonly DatabaseContext _databaseContext;
    private readonly IMediator _mediator;

    /// <summary>
    /// Initializes a new instance of the <see cref="QueryAccessibleAppListCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext"></param>
    /// <param name="mediator"></param>
    public QueryAccessibleAppListCommandHandler(DatabaseContext databaseContext, IMediator mediator)
    {
        _databaseContext = databaseContext;
        _mediator = mediator;
    }

    /// <inheritdoc/>
    public async Task<QueryAccessibleAppListCommandResponse> Handle(QueryAccessibleAppListCommand request, CancellationToken cancellationToken)
    {
        // 获取用户所在的所有团队 ID
        var teamQuery = _databaseContext.TeamUsers.AsQueryable();
        if (request.TeamId.GetValueOrDefault() > 0)
        {
            teamQuery = teamQuery.Where(x => x.TeamId == request.TeamId);
        }

        var userTeamIds = await teamQuery
            .Where(x => x.UserId == request.ContextUserId)
            .Select(x => x.TeamId)
            .ToListAsync(cancellationToken);

        // 查询公开应用 或 用户所在团队的应用
        var query = _databaseContext.Apps
            .Join(
                _databaseContext.Teams,
                app => app.TeamId,
                team => team.Id,
                (app, team) => new { app, team })
            .Where(x => !x.app.IsDisable && !x.app.IsForeign && (x.app.IsPublic || userTeamIds.Contains(x.app.TeamId)));

        // 按分类 ID 筛选
        if (request.ClassifyId.HasValue)
        {
            query = query.Where(x => x.app.ClassifyId == request.ClassifyId.Value);
        }

        // 按名称模糊搜索
        if (!string.IsNullOrWhiteSpace(request.Name))
        {
            query = query.Where(x => x.app.Name.Contains(request.Name));
        }

        var apps = await query
            .OrderByDescending(x => x.app.UpdateTime)
            .Select(x => new AccessibleAppItem
            {
                Id = x.app.Id,
                Name = x.app.Name,
                Description = x.app.Description,
                AvatarKey = x.app.Avatar,
                AppType = (AppType)x.app.AppType,
                TeamId = x.app.TeamId,
                TeamName = x.team.Name,
                IsPublic = x.app.IsPublic,
                CreateTime = x.app.CreateTime,
                UpdateTime = x.app.UpdateTime,
            })
            .ToListAsync(cancellationToken);

        await _mediator.Send(new QueryAvatarUrlCommand { Items = apps }, cancellationToken);

        return new QueryAccessibleAppListCommandResponse { Items = apps };
    }
}
