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
/// <inheritdoc cref="QueryUserViewAiModelListCommand"/>
/// </summary>
public class QueryUserViewAiModelListCommandHandler : IRequestHandler<QueryUserViewAiModelListCommand, QueryUserViewAiModelListCommandResponse>
{
    private readonly DatabaseContext _dbContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="QueryUserViewAiModelListCommandHandler"/> class.
    /// </summary>
    /// <param name="dbContext"></param>
    public QueryUserViewAiModelListCommandHandler(DatabaseContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <inheritdoc/>
    public async Task<QueryUserViewAiModelListCommandResponse> Handle(QueryUserViewAiModelListCommand request, CancellationToken cancellationToken)
    {
        var query = _dbContext.AiModels.Where(x => x.IsPublic);

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

        return new QueryUserViewAiModelListCommandResponse
        {
            AiModels = list
        };
    }
}
