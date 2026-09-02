using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.Database;
using MoAI.Database.Enums;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Models;
using MoAI.Infra.Services;
using MoAI.Storage.Services;
using MoAI.Team.Queries;
using MoAI.Team.Queries.Responses;
using MoAI.Team.Services;

namespace MoAI.Team.Handlers;

/// <summary>
/// <inheritdoc cref="QueryTeamUsersCommand"/>
/// </summary>
public class QueryTeamUsersCommandHandler : IRequestHandler<QueryTeamUsersCommand, QueryTeamUsersCommandResponse>
{
    private readonly DatabaseContext _databaseContext;
    private readonly IStorageService _storageService;
    private readonly ITeamService _teamService;
    private readonly IUserContextProvider _userContextProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="QueryTeamUsersCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    /// <param name="storageService">存储领域服务.</param>
    /// <param name="teamService">团队领域服务.</param>
    /// <param name="userContextProvider">用户上下文提供者.</param>
    public QueryTeamUsersCommandHandler(DatabaseContext databaseContext, IStorageService storageService, ITeamService teamService, IUserContextProvider userContextProvider)
    {
        _databaseContext = databaseContext;
        _storageService = storageService;
        _teamService = teamService;
        _userContextProvider = userContextProvider;
    }

    /// <inheritdoc/>
    public async Task<QueryTeamUsersCommandResponse> Handle(QueryTeamUsersCommand request, CancellationToken cancellationToken)
    {
        var userId = _userContextProvider.GetUserContext().UserId;
        var myRole = await _teamService.GetMyRoleAsync(request.TeamId, userId, cancellationToken);

        if (myRole == null)
        {
            throw new BusinessException("团队不存在或你不是团队成员.") { StatusCode = 404 };
        }

        var items = await _databaseContext.TeamUsers
            .Where(x => x.TeamId == request.TeamId)
            .Join(_databaseContext.Users, tu => tu.UserId, u => u.Id, (tu, u) => new { tu.Role, tu.CreateTime, User = u })
            .OrderBy(x => x.User.Id)
            .Select(x => new
            {
                x.User.Id,
                x.User.UserName,
                x.User.NickName,
                x.User.AvatarPath,
                Role = (int)x.Role,
                x.CreateTime
            })
            .ToListAsync(cancellationToken);

        return new QueryTeamUsersCommandResponse
        {
            Items = items.Select(x => new TeamUserItem
            {
                UserId = x.Id,
                UserName = x.UserName,
                NickName = x.NickName,
                Avatar = string.IsNullOrWhiteSpace(x.AvatarPath)
                    ? string.Empty
                    : _storageService.GetPublicFileUrl(x.AvatarPath).ToString(),
                Role = x.Role,
                JoinTime = x.CreateTime
            }).ToList()
        };
    }
}
