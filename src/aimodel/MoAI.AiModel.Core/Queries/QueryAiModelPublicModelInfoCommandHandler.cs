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
/// <inheritdoc cref="QueryAiModelPublicModelInfoCommand"/>
/// </summary>
public class QueryAiModelPublicModelInfoCommandHandler : IRequestHandler<QueryAiModelPublicModelInfoCommand, PublicModelInfo>
{
    private readonly DatabaseContext _dbContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="QueryAiModelPublicModelInfoCommandHandler"/> class.
    /// </summary>
    /// <param name="dbContext"></param>
    public QueryAiModelPublicModelInfoCommandHandler(DatabaseContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <inheritdoc/>
    public async Task<PublicModelInfo> Handle(QueryAiModelPublicModelInfoCommand request, CancellationToken cancellationToken)
    {
        var model = await _dbContext.AiModels
            .Where(x => x.IsPublic && x.Id == request.AiModelId)
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
            })
            .FirstOrDefaultAsync(cancellationToken: cancellationToken);
        if (model == null)
        {
            throw new BusinessException("AI 模型未找到或不可用.") { StatusCode = 404 };
        }

        return model;
    }
}
