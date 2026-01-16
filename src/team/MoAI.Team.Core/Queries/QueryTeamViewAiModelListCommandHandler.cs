using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.AI.Models;
using MoAI.AiModel.Models;
using MoAI.Database;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Extensions;
using MoAI.Team.Queries.Responses;
using static Microsoft.EntityFrameworkCore.DbLoggerCategory;

namespace MoAI.Team.Queries;

/// <summary>
/// <inheritdoc cref="QueryTeamViewAiModelListCommand"/>
/// </summary>
public class QueryTeamViewAiModelListCommandHandler : IRequestHandler<QueryTeamViewAiModelListCommand, QueryTeamViewAiModelListCommandResponse>
{
    private readonly DatabaseContext _dbContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="QueryTeamViewAiModelListCommandHandler"/> class.
    /// </summary>
    /// <param name="dbContext">数据库上下文.</param>
    public QueryTeamViewAiModelListCommandHandler(DatabaseContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <inheritdoc/>
    public async Task<QueryTeamViewAiModelListCommandResponse> Handle(QueryTeamViewAiModelListCommand request, CancellationToken cancellationToken)
    {
        var authorizedModelIds = _dbContext.AiModelAuthorizations
            .Where(a => a.TeamId == request.TeamId)
            .Select(a => a.AiModelId);

        var query = _dbContext.AiModels.Where(x => x.IsPublic || authorizedModelIds.Contains(x.Id));

        if (request.AiModelType != null)
        {
            query = query.Where(x => x.AiModelType == request.AiModelType.ToJsonString());
        }

        var list = await query
            .Select(x => new PublicModelInfo
            {
                Id = x.Id,
                Name = x.Name,
                Title = x.Title,
                AiModelType = x.AiModelType.JsonToObject<AiModelType>(),
                ContextWindowTokens = x.ContextWindowTokens,
                Abilities = new ModelAbilities
                {
                    Files = x.Files,
                    FunctionCall = x.FunctionCall,
                    ImageOutput = x.ImageOutput,
                    Vision = x.IsVision,
                },
                MaxDimension = x.MaxDimension,
                TextOutput = x.TextOutput,
                IsAuthorization = x.IsPublic ? false : true,
            }).ToArrayAsync(cancellationToken: cancellationToken);

        return new QueryTeamViewAiModelListCommandResponse
        {
            AiModels = list
        };
    }
}
