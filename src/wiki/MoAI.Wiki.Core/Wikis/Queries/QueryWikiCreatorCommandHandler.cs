using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.Database;
using MoAI.Database.Entities;
using MoAI.Team.Queries;
using MoAI.Wiki.Wikis.Queries;
using MoAI.Wiki.Wikis.Queries.Response;

namespace MoAI.Plugin.Queries;

/// <summary>
/// <inheritdoc cref="QueryWikiCreatorCommandHandler"/>
/// </summary>
public class QueryWikiCreatorCommandHandler : IRequestHandler<QueryWikiCreatorCommand, QueryWikiCreatorCommandResponse>
{
    private readonly DatabaseContext _databaseContext;
    private readonly IMediator _mediator;

    /// <summary>
    /// Initializes a new instance of the <see cref="QueryWikiCreatorCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext"></param>
    /// <param name="mediator"></param>
    public QueryWikiCreatorCommandHandler(DatabaseContext databaseContext, IMediator mediator)
    {
        _databaseContext = databaseContext;
        _mediator = mediator;
    }

    /// <inheritdoc/>
    public async Task<QueryWikiCreatorCommandResponse> Handle(QueryWikiCreatorCommand request, CancellationToken cancellationToken)
    {
        var wikiEntity = await _databaseContext.Wikis.Where(x => x.Id == request.WikiId)
            .AsNoTracking()
            .Select(x => new
            {
                Id = x.Id,
                IsPublic = x.IsPublic,
                CreateUserId = x.CreateUserId,
                TeamId = x.TeamId,
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (wikiEntity == null)
        {
            return new QueryWikiCreatorCommandResponse
            {
                IsExist = false,
                TeamRole = Team.Models.TeamRole.None
            };
        }

        // 个人知识库
        if (wikiEntity.TeamId == 0)
        {
            return new QueryWikiCreatorCommandResponse
            {
                WikiId = wikiEntity.Id,
                IsExist = true,
                CreatorId = wikiEntity.CreateUserId,
                IsTeam = false,
                TeamRole = wikiEntity.CreateUserId == request.ContextUserId ? Team.Models.TeamRole.Owner : Team.Models.TeamRole.Collaborator,
                IsPublic = wikiEntity.IsPublic
            };
        }

        // 查询用户是否在团队中
        var userInTeam = await _mediator.Send(
            new QueryUserTeamRoleCommand
            {
                TeamId = wikiEntity.TeamId,
                ContextUserId = request.ContextUserId
            },
            cancellationToken);

        return new QueryWikiCreatorCommandResponse
        {
            IsExist = true,
            WikiId = wikiEntity.Id,
            TeamRole = userInTeam.Role,
            CreatorId = wikiEntity.TeamId,
            IsPublic = wikiEntity.IsPublic,
            IsTeam = true
        };
    }
}
