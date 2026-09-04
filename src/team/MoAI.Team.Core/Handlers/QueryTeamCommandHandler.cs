using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.Database;
using MoAI.Database.Enums;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Models;
using MoAI.Storage.Services;
using MoAI.Team.Queries;
using MoAI.Team.Queries.Responses;

namespace MoAI.Team.Handlers;

/// <summary>
/// <inheritdoc cref="QueryTeamCommand"/>
/// </summary>
public class QueryTeamCommandHandler : IRequestHandler<QueryTeamCommand, QueryTeamCommandResponse>
{
    private readonly DatabaseContext _databaseContext;
    private readonly IStorageService _storageService;

    /// <summary>
    /// Initializes a new instance of the <see cref="QueryTeamCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    /// <param name="storageService">存储领域服务.</param>
    public QueryTeamCommandHandler(DatabaseContext databaseContext, IStorageService storageService)
    {
        _databaseContext = databaseContext;
        _storageService = storageService;
    }

    /// <inheritdoc/>
    public async Task<QueryTeamCommandResponse> Handle(QueryTeamCommand request, CancellationToken cancellationToken)
    {
        var team = await _databaseContext.Teams
            .FirstOrDefaultAsync(x => x.Id == request.TeamId, cancellationToken);

        if (team == null)
        {
            throw new BusinessException("团队不存在.") { StatusCode = 404 };
        }

        // 查询团队负责人（Owner）的用户信息
        var owner = await _databaseContext.TeamUsers
            .Where(x => x.TeamId == request.TeamId && x.Role == (int)TeamRole.Owner)
            .Join(_databaseContext.Users, tu => tu.UserId, u => u.Id, (tu, u) => new { u.Id, u.UserName, u.NickName, u.AvatarPath })
            .FirstOrDefaultAsync(cancellationToken);

        // 我在团队中的角色
        var myRole = await _databaseContext.TeamUsers
            .Where(x => x.TeamId == request.TeamId && x.UserId == request.ContextUserId)
            .Select(x => (int?)x.Role)
            .FirstOrDefaultAsync(cancellationToken);

        return new QueryTeamCommandResponse
        {
            TeamId = team.Id,
            Name = team.Name,
            Description = team.Description,
            Avatar = string.IsNullOrWhiteSpace(team.AvatarPath)
                ? string.Empty
                : _storageService.GetPublicFileUrl(team.AvatarPath).ToString(),
            IsDisable = team.IsDisable,
            MyRole = myRole ?? 0,
            CreateTime = team.CreateTime,
            OwnerUserId = owner?.Id ?? default,
            OwnerUserName = owner?.UserName ?? string.Empty,
            OwnerNickName = owner?.NickName ?? string.Empty,
            OwnerAvatar = string.IsNullOrWhiteSpace(owner?.AvatarPath)
                ? string.Empty
                : _storageService.GetPublicFileUrl(owner!.AvatarPath).ToString()
        };
    }
}
