using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.Account.Services;
using MoAI.Database;
using MoAI.Database.Enums;
using MoAI.Storage.Services;
using MoAI.Team.Queries;
using MoAI.Team.Queries.Responses;

namespace MoAI.Team.Handlers;

/// <summary>
/// <inheritdoc cref="QueryAdminTeamListCommand"/>
/// </summary>
public class QueryAdminTeamListCommandHandler : IRequestHandler<QueryAdminTeamListCommand, QueryAdminTeamListCommandResponse>
{
    private readonly DatabaseContext _databaseContext;
    private readonly IStorageService _storageService;
    private readonly IUserInfoFillService _userInfoFillService;

    /// <summary>
    /// Initializes a new instance of the <see cref="QueryAdminTeamListCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    /// <param name="storageService">存储领域服务.</param>
    /// <param name="userInfoFillService">用户信息填充服务.</param>
    public QueryAdminTeamListCommandHandler(DatabaseContext databaseContext, IStorageService storageService, IUserInfoFillService userInfoFillService)
    {
        _databaseContext = databaseContext;
        _storageService = storageService;
        _userInfoFillService = userInfoFillService;
    }

    /// <inheritdoc/>
    public async Task<QueryAdminTeamListCommandResponse> Handle(QueryAdminTeamListCommand request, CancellationToken cancellationToken)
    {
        var query = _databaseContext.Teams.AsQueryable();

        if (!string.IsNullOrWhiteSpace(request.SearchText))
        {
            var keyword = request.SearchText.Trim();
            query = query.Where(x => x.Name.Contains(keyword) || x.Description.Contains(keyword));
        }

        if (request.IsDisable != null)
        {
            query = query.Where(x => x.IsDisable == request.IsDisable);
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var teams = await query
            .OrderBy(x => x.Id)
            .Skip(request.Skip)
            .Take(request.Take)
            .Select(x => new
            {
                x.Id,
                x.Name,
                x.Description,
                x.AvatarPath,
                x.IsDisable,
                x.CreateUserId,
                x.CreateTime,
                x.UpdateUserId,
                x.UpdateTime
            })
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

        var items = teams.Select(x =>
        {
            owners.TryGetValue(x.Id, out var owner);
            return new QueryAdminTeamListCommandResponseItem
            {
                TeamId = x.Id,
                Name = x.Name,
                Description = x.Description,
                Avatar = string.IsNullOrWhiteSpace(x.AvatarPath)
                    ? string.Empty
                    : _storageService.GetPublicFileUrl(x.AvatarPath).ToString(),
                IsDisable = x.IsDisable,
                MemberCount = counts.GetValueOrDefault(x.Id),
                OwnerUserId = owner?.Id ?? default,
                OwnerUserName = owner?.UserName ?? string.Empty,
                OwnerNickName = owner?.NickName ?? string.Empty,
                OwnerAvatar = string.IsNullOrWhiteSpace(owner?.AvatarPath)
                    ? string.Empty
                    : _storageService.GetPublicFileUrl(owner!.AvatarPath).ToString(),
                CreateUserId = (int)x.CreateUserId,
                UpdateUserId = (int)x.UpdateUserId,
                CreateTime = x.CreateTime,
                UpdateTime = x.UpdateTime,
            };
        }).ToList();

        await _userInfoFillService.FillAsync(items, cancellationToken);

        return new QueryAdminTeamListCommandResponse
        {
            TotalCount = totalCount,
            Items = items,
        };
    }
}
