using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.AiModel.Authorization.Queries.Responses;
using MoAI.Database;

namespace MoAI.AiModel.Authorization.Queries;

/// <summary>
/// <inheritdoc cref="QueryTeamAuthorizationsCommand"/>
/// </summary>
public class QueryTeamAuthorizationsCommandHandler : IRequestHandler<QueryTeamAuthorizationsCommand, QueryTeamAuthorizationsCommandResponse>
{
    private readonly DatabaseContext _dbContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="QueryTeamAuthorizationsCommandHandler"/> class.
    /// </summary>
    /// <param name="dbContext"></param>
    public QueryTeamAuthorizationsCommandHandler(DatabaseContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <inheritdoc/>
    public async Task<QueryTeamAuthorizationsCommandResponse> Handle(QueryTeamAuthorizationsCommand request, CancellationToken cancellationToken)
    {
        var query = _dbContext.Teams.AsQueryable();

        var teams = await query
            .Select(x => new
            {
                x.Id,
                x.Name
            })
            .ToListAsync(cancellationToken);

        var teamIds = teams.Select(x => x.Id).ToList();

        var authorizations = await _dbContext.AiModelAuthorizations
            .Where(x => teamIds.Contains(x.TeamId))
            .Join(
            _dbContext.AiModels,
            auth => auth.AiModelId,
            model => model.Id,
            (auth, model) => new
            {
                auth.Id,
                auth.TeamId,
                ModelId = model.Id,
                ModelName = model.Name,
                ModelTitle = model.Title
            })
            .ToListAsync(cancellationToken);

        var result = teams.Select(team => new TeamAuthorizationItem
        {
            TeamId = team.Id,
            TeamName = team.Name,
            AuthorizedModels = authorizations
                .Where(x => x.TeamId == team.Id)
                .Select(x => new AuthorizedModelItem
                {
                    ModelId = x.ModelId,
                    Name = x.ModelName,
                    Title = x.ModelTitle,
                    AuthorizationId = x.Id
                })
                .ToList()
        }).ToList();

        return new QueryTeamAuthorizationsCommandResponse
        {
            Teams = result
        };
    }
}
