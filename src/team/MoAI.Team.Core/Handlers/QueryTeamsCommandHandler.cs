using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.Database;
using MoAI.Database.Enums;
using MoAI.Infra.Services;
using MoAI.Storage.Services;
using MoAI.Team.Queries;
using MoAI.Team.Queries.Responses;

namespace MoAI.Team.Handlers;

/// <summary>
/// <inheritdoc cref="QueryTeamsCommand"/>
/// </summary>
public class QueryTeamsCommandHandler : IRequestHandler<QueryTeamsCommand, QueryTeamsCommandResponse>
{
    private readonly DatabaseContext _databaseContext;
    private readonly IStorageService _storageService;
    private readonly IUserContextProvider _userContextProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="QueryTeamsCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    /// <param name="storageService">存储领域服务.</param>
    /// <param name="userContextProvider">用户上下文提供者.</param>
    public QueryTeamsCommandHandler(DatabaseContext databaseContext, IStorageService storageService, IUserContextProvider userContextProvider)
    {
        _databaseContext = databaseContext;
        _storageService = storageService;
        _userContextProvider = userContextProvider;
    }

    /// <inheritdoc/>
    public async Task<QueryTeamsCommandResponse> Handle(QueryTeamsCommand request, CancellationToken cancellationToken)
    {
        var userId = _userContextProvider.GetUserContext().UserId;

        var teams = await _databaseContext.TeamUsers
            .Where(x => x.UserId == userId)
            .Join(_databaseContext.Teams, tu => tu.TeamId, t => t.Id, (tu, t) => new { tu.Role, Team = t })
            .Select(x => new
            {
                x.Team.Id,
                x.Team.Name,
                x.Team.Description,
                x.Team.AvatarPath,
                x.Team.IsDisable,
                x.Team.CreateTime,
                MyRole = (int)x.Role
            })
            .OrderBy(x => x.Id)
            .ToListAsync(cancellationToken);

        var teamIds = teams.Select(x => x.Id).ToList();

        var counts = await _databaseContext.TeamUsers
            .Where(x => teamIds.Contains(x.TeamId))
            .GroupBy(x => x.TeamId)
            .Select(g => new { TeamId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.TeamId, x => x.Count, cancellationToken);

        return new QueryTeamsCommandResponse
        {
            Items = teams.Select(x => new TeamItem
            {
                TeamId = x.Id,
                Name = x.Name,
                Description = x.Description,
                Avatar = string.IsNullOrWhiteSpace(x.AvatarPath)
                    ? string.Empty
                    : _storageService.GetPublicFileUrl(x.AvatarPath).ToString(),
                IsDisable = x.IsDisable,
                MyRole = x.MyRole,
                MemberCount = counts.GetValueOrDefault(x.Id),
                CreateTime = x.CreateTime
            }).ToList()
        };
    }
}
