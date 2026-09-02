using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.AIChannel.Queries;
using MoAI.AIChannel.Queries.Responses;
using MoAI.Database;

namespace MoAI.AIChannel.Queries;

/// <summary>
/// <inheritdoc cref="QueryAIModelListCommand"/>
/// </summary>
public class QueryAIModelListCommandHandler : IRequestHandler<QueryAIModelListCommand, QueryAIModelListCommandResponse>
{
    private readonly DatabaseContext _databaseContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="QueryAIModelListCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext"></param>
    public QueryAIModelListCommandHandler(DatabaseContext databaseContext)
    {
        _databaseContext = databaseContext;
    }

    /// <inheritdoc/>
    public async Task<QueryAIModelListCommandResponse> Handle(QueryAIModelListCommand request, CancellationToken cancellationToken)
    {
        var query = _databaseContext.AiModels
            .Where(x => x.IsDeleted == 0);

        if (request.ChannelId != null)
        {
            query = query.Where(x => x.ChannelId == request.ChannelId.Value);
        }

        var items = await query
            .Select(x => new QueryAIModelListCommandResponseItem
            {
                Id = x.Id,
                ChannelId = x.ChannelId,
                ModelId = x.ModelId,
                Name = x.Name,
                Description = x.Description,
                Family = x.Family,
                ModelKind = x.ModelKind,
                SupportsVision = x.SupportsVision,
                SupportsAttachments = x.SupportsAttachments,
                SupportsReasoning = x.SupportsReasoning,
                SupportsToolCall = x.SupportsToolCall,
                SupportsStructuredOutput = x.SupportsStructuredOutput,
                SupportsTemperature = x.SupportsTemperature,
                ContextWindow = x.ContextWindow,
                MaxOutput = x.MaxOutput,
                ModalitiesInput = x.ModalitiesInput,
                ModalitiesOutput = x.ModalitiesOutput,
                KnowledgeCutoff = x.KnowledgeCutoff,
                ReleaseDate = x.ReleaseDate,
                LastUpdated = x.LastUpdated,
                OpenWeights = x.OpenWeights,
                CostInput = x.CostInput,
                CostOutput = x.CostOutput,
                CostCacheRead = x.CostCacheRead,
                Enabled = x.Enabled,
                CreateTime = x.CreateTime,
                CreateUserId = (int)x.CreateUserId,
                UpdateTime = x.UpdateTime,
                UpdateUserId = (int)x.UpdateUserId,
            })
            .ToListAsync(cancellationToken);

        return new QueryAIModelListCommandResponse { Items = items };
    }
}
