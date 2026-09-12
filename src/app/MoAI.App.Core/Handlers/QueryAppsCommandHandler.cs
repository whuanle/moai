using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.App.Queries;
using MoAI.App.Queries.Responses;
using MoAI.Database;
using MoAI.Database.Enums;
using MoAI.Infra.Exceptions;
using MoAI.Team.Services;

namespace MoAI.App.Handlers;

/// <summary>
/// <inheritdoc cref="QueryAppsCommand"/>
/// </summary>
public class QueryAppsCommandHandler : IRequestHandler<QueryAppsCommand, QueryAppsCommandResponse>
{
    private readonly DatabaseContext _databaseContext;
    private readonly ITeamService _teamService;

    /// <summary>
    /// Initializes a new instance of the <see cref="QueryAppsCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    /// <param name="teamService">团队领域服务.</param>
    public QueryAppsCommandHandler(DatabaseContext databaseContext, ITeamService teamService)
    {
        _databaseContext = databaseContext;
        _teamService = teamService;
    }

    /// <inheritdoc/>
    public async Task<QueryAppsCommandResponse> Handle(QueryAppsCommand request, CancellationToken cancellationToken)
    {
        var myRole = await _teamService.GetMyRoleAsync(request.TeamId, request.ContextUserId, cancellationToken);

        if (myRole == null)
        {
            throw new BusinessException("团队不存在或你不是团队成员.") { StatusCode = 404 };
        }

        // 先投影出列再在内存中映射 DTO，避免在 EF 表达式中做 int→枚举 转换
        var rows = await _databaseContext.Apps
            .Where(x => x.TeamId == request.TeamId)
            .OrderByDescending(x => x.CreateTime)
            .Select(x => new
            {
                x.Id,
                x.TeamId,
                x.Name,
                x.Description,
                x.AppType,
                x.Avatar,
                x.EnableForeign,
                x.PublishStatus,
                x.PublishTime,
                x.CreateTime
            })
            .ToListAsync(cancellationToken);

        var items = rows
            .Select(x => new AppItem
            {
                AppId = x.Id,
                TeamId = x.TeamId,
                Name = x.Name,
                Description = x.Description,
                AppType = (AppType)x.AppType,
                AvatarPath = x.Avatar,
                EnableForeign = x.EnableForeign,
                PublishStatus = x.PublishStatus,
                PublishTime = x.PublishTime,
                CreateTime = x.CreateTime
            })
            .ToList();

        return new QueryAppsCommandResponse
        {
            TeamId = request.TeamId,
            MyRole = (int)myRole.Value,
            Items = items
        };
    }
}
