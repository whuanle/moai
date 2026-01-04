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

public class QueryAiModelToAiEndpointCommandHandler : IRequestHandler<QueryAiModelToAiEndpointCommand, AiEndpoint>
{
    private readonly DatabaseContext _databaseContext;
    public QueryAiModelToAiEndpointCommandHandler(DatabaseContext databaseContext)
    {
        _databaseContext = databaseContext;
    }
    public async Task<AiEndpoint> Handle(QueryAiModelToAiEndpointCommand request, CancellationToken cancellationToken)
    {
        var query = _databaseContext.AiModels
            .Where(x => x.Id == request.AiModelId);

        if (request.IsPublic.HasValue)
        {
            query = query.Where(x => x.IsPublic == request.IsPublic.Value);
        }

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