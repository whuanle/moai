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
/// <inheritdoc cref="QueryAccessAppsCommand"/>
/// </summary>
public class QueryAccessAppsCommandHandler : IRequestHandler<QueryAccessAppsCommand, QueryAccessAppsCommandResponse>
{
    private readonly DatabaseContext _databaseContext;
    private readonly ITeamService _teamService;

    /// <summary>
    /// Initializes a new instance of the <see cref="QueryAccessAppsCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    /// <param name="teamService">团队领域服务.</param>
    public QueryAccessAppsCommandHandler(DatabaseContext databaseContext, ITeamService teamService)
    {
        _databaseContext = databaseContext;
        _teamService = teamService;
    }

    /// <inheritdoc/>
    public async Task<QueryAccessAppsCommandResponse> Handle(QueryAccessAppsCommand request, CancellationToken cancellationToken)
    {
        var myRole = await _teamService.GetMyRoleAsync(request.TeamId, request.ContextUserId, cancellationToken);

        if (myRole == null)
        {
            throw new BusinessException("团队不存在或你不是团队成员.") { StatusCode = 404 };
        }

        if (myRole == TeamRole.Member)
        {
            throw new BusinessException("只有团队管理员可以查看应用接入.") { StatusCode = 403 };
        }

        var rows = await _databaseContext.AccessApps
            .Where(x => x.TeamId == request.TeamId)
            .OrderByDescending(x => x.CreateTime)
            .Select(x => new { x.Id, x.Name, x.Description, x.Key, x.AppIds, x.CreateTime })
            .ToListAsync(cancellationToken);

        var items = rows
            .Select(x => new AccessAppItem
            {
                AccessAppId = x.Id,
                Name = x.Name,
                Description = x.Description,
                Key = x.Key,
                AppIds = x.AppIds ?? new List<Guid>(),
                CreateTime = x.CreateTime,
            })
            .ToList();

        return new QueryAccessAppsCommandResponse
        {
            TeamId = request.TeamId,
            MyRole = (int)myRole.Value,
            Items = items,
        };
    }
}
