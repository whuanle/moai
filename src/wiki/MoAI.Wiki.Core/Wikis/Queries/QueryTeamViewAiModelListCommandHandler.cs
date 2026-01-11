using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.AI.Models;
using MoAI.AiModel.Models;
using MoAI.Database;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Extensions;
using MoAI.Wiki.Wikis.Queries;
using MoAI.Wiki.Wikis.Queries.Response;

namespace MoAI.Wiki.Wikis.Queries;

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
        // 查询公开模型或团队被授权的模型
        var teamId = await _dbContext.Wikis
            .Where(w => w.Id == request.WikiId)
            .Select(x => x.TeamId)
            .FirstAsync(cancellationToken: cancellationToken);

        var query = _dbContext.AiModels.AsQueryable(); ;

        // 个人知识库
        if (teamId == 0)
        {
            query = _dbContext.AiModels.Where(x => x.IsPublic);
        }
        else
        {
            var authorizedModelIds = _dbContext.AiModelAuthorizations
                .Where(a => a.TeamId == teamId)
                .Select(a => a.AiModelId);

            query = query.Where(x => x.IsPublic || authorizedModelIds.Contains(x.Id));
        }

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
                TextOutput = x.TextOutput
            }).ToArrayAsync(cancellationToken: cancellationToken);

        return new QueryTeamViewAiModelListCommandResponse
        {
            AiModels = list
        };
    }
}
