using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.AI.Models;
using MoAI.AiModel.Models;
using MoAI.AiModel.Queries.Respones;
using MoAI.Database;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Extensions;
using MoAI.Infra.Models;

namespace MoAI.AiModel.Queries;
/// <summary>
/// <inheritdoc cref="QueryPublicAiModelToAiEndpointCommand"/>
/// </summary>
public class QueryTeamAiModelToAiEndpointCommandHandler : IRequestHandler<QueryTeamAiModelToAiEndpointCommand, AiEndpoint>
{
    private readonly DatabaseContext _databaseContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="QueryTeamAiModelToAiEndpointCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext"></param>
    public QueryTeamAiModelToAiEndpointCommandHandler(DatabaseContext databaseContext)
    {
        _databaseContext = databaseContext;
    }

    /// <inheritdoc/>
    public async Task<AiEndpoint> Handle(QueryTeamAiModelToAiEndpointCommand request, CancellationToken cancellationToken)
    {
        var query = _databaseContext.AiModels
            .Where(x => x.Id == request.AiModelId &&
            !x.IsPublic &&
            _databaseContext.AiModelAuthorizations.Where(a => a.AiModelId == request.AiModelId && a.TeamId == request.TeamId).Any());

        var aiEndpoint = await query.AsNoTracking().Select(x => new AiEndpoint
        {
            Name = x.Name,
            DeploymentName = x.DeploymentName,
            Title = x.Title,
            AiModelType = Enum.Parse<AiModelType>(x.AiModelType, true),
            Provider = Enum.Parse<AiProvider>(x.AiProvider, true),
            ContextWindowTokens = x.ContextWindowTokens,
            Endpoint = x.Endpoint,
            Key = x.Key,
            Abilities = new ModelAbilities
            {
                Files = x.Files,
                FunctionCall = x.FunctionCall,
                ImageOutput = x.ImageOutput,
                Vision = x.IsVision,
            },
            MaxDimension = x.MaxDimension,
        }).FirstOrDefaultAsync();

        if (aiEndpoint == null)
        {
            throw new BusinessException("AI 模型未找到或不可用.") { StatusCode = 404 };
        }

        return aiEndpoint;
    }
}