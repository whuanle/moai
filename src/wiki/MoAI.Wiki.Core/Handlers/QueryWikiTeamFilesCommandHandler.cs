using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.Database;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Services;
using MoAI.Team.Services;
using MoAI.Wiki.Queries;
using MoAI.Wiki.Queries.Responses;

namespace MoAI.Wiki.Handlers;

/// <summary>
/// <inheritdoc cref="QueryWikiTeamFilesCommand"/>
/// </summary>
public class QueryWikiTeamFilesCommandHandler : IRequestHandler<QueryWikiTeamFilesCommand, QueryWikiTeamFilesCommandResponse>
{
    private readonly DatabaseContext _databaseContext;
    private readonly ITeamService _teamService;
    private readonly IUserContextProvider _userContextProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="QueryWikiTeamFilesCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    /// <param name="teamService">团队领域服务.</param>
    /// <param name="userContextProvider">用户上下文提供者.</param>
    public QueryWikiTeamFilesCommandHandler(DatabaseContext databaseContext, ITeamService teamService, IUserContextProvider userContextProvider)
    {
        _databaseContext = databaseContext;
        _teamService = teamService;
        _userContextProvider = userContextProvider;
    }

    /// <inheritdoc/>
    public async Task<QueryWikiTeamFilesCommandResponse> Handle(QueryWikiTeamFilesCommand request, CancellationToken cancellationToken)
    {
        var userId = _userContextProvider.GetUserContext().UserId;
        var myRole = await _teamService.GetMyRoleAsync(request.TeamId, userId, cancellationToken);

        if (myRole == null)
        {
            throw new BusinessException("团队不存在或你不是团队成员.") { StatusCode = 404 };
        }

        var keyword = request.Query.Trim();

        var query = from wiki in _databaseContext.Wikis
                    join document in _databaseContext.WikiDocuments on wiki.Id equals document.WikiId
                    where wiki.TeamId == request.TeamId
                        && wiki.IsDeleted == 0
                        && document.IsDeleted == 0
                        && document.FileName.Contains(keyword)
                    select new WikiTeamFileItem
                    {
                        DocumentId = document.Id,
                        WikiId = wiki.Id,
                        WikiName = wiki.Name,
                        FileName = document.FileName,
                        UpdateTime = document.UpdateTime
                    };

        var total = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(x => x.FileName == keyword)
            .ThenByDescending(x => x.FileName.StartsWith(keyword))
            .ThenByDescending(x => x.UpdateTime)
            .Take(request.Limit)
            .ToListAsync(cancellationToken);

        return new QueryWikiTeamFilesCommandResponse
        {
            TeamId = request.TeamId,
            Total = total,
            Items = items
        };
    }
}
