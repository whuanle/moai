using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.Database;
using MoAI.Storage.Services;
using MoAI.Team.Queries;
using MoAI.Team.Queries.Responses;

namespace MoAI.Team.Handlers;

/// <summary>
/// <inheritdoc cref="QueryTeamCandidatesCommand"/>
/// </summary>
public class QueryTeamCandidatesCommandHandler : IRequestHandler<QueryTeamCandidatesCommand, QueryTeamCandidatesCommandResponse>
{
    private readonly DatabaseContext _databaseContext;
    private readonly IStorageService _storageService;

    /// <summary>
    /// Initializes a new instance of the <see cref="QueryTeamCandidatesCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    /// <param name="storageService">存储领域服务.</param>
    public QueryTeamCandidatesCommandHandler(DatabaseContext databaseContext, IStorageService storageService)
    {
        _databaseContext = databaseContext;
        _storageService = storageService;
    }

    /// <inheritdoc/>
    public async Task<QueryTeamCandidatesCommandResponse> Handle(QueryTeamCandidatesCommand request, CancellationToken cancellationToken)
    {
        var existingMemberIds = await _databaseContext.TeamUsers
            .Where(x => x.TeamId == request.TeamId && x.IsDeleted == 0)
            .Select(x => x.UserId)
            .ToHashSetAsync(cancellationToken);

        var keyword = string.IsNullOrWhiteSpace(request.Keyword) ? string.Empty : request.Keyword.Trim();

        var users = await _databaseContext.Users
            .Where(u => u.IsDeleted == 0 && !existingMemberIds.Contains(u.Id))
            .Where(u => keyword == string.Empty ||
                        u.UserName.Contains(keyword) ||
                        u.NickName.Contains(keyword) ||
                        u.Email.Contains(keyword))
            .OrderBy(u => u.Id)
            .Take(20)
            .Select(u => new
            {
                u.Id,
                u.UserName,
                u.NickName,
                u.AvatarPath
            })
            .ToListAsync(cancellationToken);

        return new QueryTeamCandidatesCommandResponse
        {
            Items = users.Select(u => new TeamCandidateItem
            {
                UserId = u.Id,
                UserName = u.UserName,
                NickName = u.NickName,
                Avatar = string.IsNullOrWhiteSpace(u.AvatarPath)
                    ? string.Empty
                    : _storageService.GetPublicFileUrl(u.AvatarPath).ToString()
            }).ToList()
        };
    }
}
