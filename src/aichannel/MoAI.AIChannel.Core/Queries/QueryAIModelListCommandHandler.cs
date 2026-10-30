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
        var query = _databaseContext.AiModels.AsQueryable();

        if (request.OwnerTeamId > 0)
        {
            var ownerTeamId = request.OwnerTeamId;
            query = query.Where(x => _databaseContext.AiChannels
                .Any(c => c.Id == x.ChannelId && c.TeamId == ownerTeamId));
        }
        else if (request.TeamId.HasValue)
        {
            var teamId = request.TeamId.Value;
            query = query.Where(x => x.IsPublic || _databaseContext.AiModelAuthorizations
                .Any(a => a.AiModelId == x.Id && a.TeamId == teamId));
        }

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
                IsPublic = x.IsPublic,
                CreateTime = x.CreateTime,
                CreateUserId = (int)x.CreateUserId,
                UpdateTime = x.UpdateTime,
                UpdateUserId = (int)x.UpdateUserId,
            })
            .ToListAsync(cancellationToken);

        // 批量填充授权团队名称（私有模型的授权团队，公开模型为空集合）
        var modelIds = items.Select(x => x.Id).ToList();
        var authPairs = await _databaseContext.AiModelAuthorizations
            .Where(x => modelIds.Contains(x.AiModelId))
            .Join(_databaseContext.Teams, a => a.TeamId, t => t.Id, (a, t) => new { a.AiModelId, a.TeamId, t.Name })
            .OrderBy(x => x.TeamId)
            .ToListAsync(cancellationToken);
        var authNamesByModelId = authPairs
            .GroupBy(x => x.AiModelId)
            .ToDictionary(g => g.Key, g => g.Select(x => x.Name).ToList());
        foreach (var item in items)
        {
            item.AuthorizedTeamNames = authNamesByModelId.GetValueOrDefault(item.Id) ?? new List<string>();
        }

        return new QueryAIModelListCommandResponse { Items = items };
    }
}
