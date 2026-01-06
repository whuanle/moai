using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.AI.Models;
using MoAI.AiModel.Authorization.Queries.Responses;
using MoAI.Database;
using MoAI.Infra.Extensions;
using MoAI.Infra.Models;

namespace MoAI.AiModel.Authorization.Queries;

/// <summary>
/// <inheritdoc cref="QueryModelAuthorizationsCommand"/>
/// </summary>
public class QueryModelAuthorizationsCommandHandler : IRequestHandler<QueryModelAuthorizationsCommand, QueryModelAuthorizationsCommandResponse>
{
    private readonly DatabaseContext _dbContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="QueryModelAuthorizationsCommandHandler"/> class.
    /// </summary>
    /// <param name="dbContext"></param>
    public QueryModelAuthorizationsCommandHandler(DatabaseContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <inheritdoc/>
    public async Task<QueryModelAuthorizationsCommandResponse> Handle(QueryModelAuthorizationsCommand request, CancellationToken cancellationToken)
    {
        var query = _dbContext.AiModels.Where(x => x.IsPublic == false);

        var models = await query
            .Select(x => new
            {
                x.Id,
                x.Name,
                x.Title,
                x.AiModelType,
                x.AiProvider,
                x.IsPublic
            })
            .ToListAsync(cancellationToken);

        var modelIds = models.Select(x => x.Id).ToList();

        var authorizations = await _dbContext.AiModelAuthorizations
            .Where(x => modelIds.Contains(x.AiModelId))
            .Join(
                _dbContext.Teams,
                auth => auth.TeamId,
                team => team.Id,
                (auth, team) => new
                {
                    auth.Id,
                    auth.AiModelId,
                    TeamId = team.Id,
                    TeamName = team.Name
                })
            .ToListAsync(cancellationToken);


        var result = models.Select(model => new ModelAuthorizationItem
        {
            ModelId = model.Id,
            Name = model.Name,
            Title = model.Title,
            AiModelType = model.AiModelType.JsonToObject<AiModelType>(),
            AiProvider = model.AiProvider.JsonToObject<AiProvider>(),
            AuthorizedTeams = authorizations
                .Where(x => x.AiModelId == model.Id)
                .Select(x => new AuthorizedTeamItem
                {
                    TeamId = x.TeamId,
                    TeamName = x.TeamName,
                    AuthorizationId = x.Id,
                })
                .ToList()
        }).ToList();

        return new QueryModelAuthorizationsCommandResponse
        {
            Models = result
        };
    }
}
