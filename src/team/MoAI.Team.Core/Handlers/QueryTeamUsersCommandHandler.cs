using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.Database;
using MoAI.Database.Enums;
using MoAI.Storage.Services;
using MoAI.Team.Queries;
using MoAI.Team.Queries.Responses;

namespace MoAI.Team.Handlers;

/// <summary>
/// <inheritdoc cref="QueryTeamUsersCommand"/>
/// </summary>
public class QueryTeamUsersCommandHandler : IRequestHandler<QueryTeamUsersCommand, QueryTeamUsersCommandResponse>
{
    private readonly DatabaseContext _databaseContext;
    private readonly IStorageService _storageService;

    /// <summary>
    /// Initializes a new instance of the <see cref="QueryTeamUsersCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    /// <param name="storageService">存储领域服务.</param>
    public QueryTeamUsersCommandHandler(DatabaseContext databaseContext, IStorageService storageService)
    {
        _databaseContext = databaseContext;
        _storageService = storageService;
    }

    /// <inheritdoc/>
    public async Task<QueryTeamUsersCommandResponse> Handle(QueryTeamUsersCommand request, CancellationToken cancellationToken)
    {
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
