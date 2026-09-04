using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.Database;
using MoAI.Database.Enums;
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

    /// <summary>
    /// Initializes a new instance of the <see cref="QueryTeamsCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    /// <param name="storageService">存储领域服务.</param>
    public QueryTeamsCommandHandler(DatabaseContext databaseContext, IStorageService storageService)
    {
        _databaseContext = databaseContext;
        _storageService = storageService;
    }

    /// <inheritdoc/>
    public async Task<QueryTeamsCommandResponse> Handle(QueryTeamsCommand request, CancellationToken cancellationToken)
    {
        var teams = await _databaseContext.TeamUsers
            .Where(x => x.UserId == request.ContextUserId)
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

        // 查询各团队负责人（Owner）的用户信息
        var owners = await _databaseContext.TeamUsers
            .Where(x => teamIds.Contains(x.TeamId) && x.Role == (int)TeamRole.Owner)
            .Join(_databaseContext.Users, tu => tu.UserId, u => u.Id, (tu, u) => new { tu.TeamId, u.Id, u.UserName, u.NickName, u.AvatarPath })
            .ToDictionaryAsync(x => x.TeamId, x => x, cancellationToken);

        return new QueryTeamsCommandResponse
        {
            Items = teams.Select(x =>
            {
                owners.TryGetValue(x.Id, out var owner);
                return new TeamItem
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
                    CreateTime = x.CreateTime,
                    OwnerUserId = owner?.Id ?? default,
                    OwnerUserName = owner?.UserName ?? string.Empty,
                    OwnerNickName = owner?.NickName ?? string.Empty,
                    OwnerAvatar = string.IsNullOrWhiteSpace(owner?.AvatarPath)
                        ? string.Empty
                        : _storageService.GetPublicFileUrl(owner!.AvatarPath).ToString()
                };
            }).ToList()
        };
    }
}
