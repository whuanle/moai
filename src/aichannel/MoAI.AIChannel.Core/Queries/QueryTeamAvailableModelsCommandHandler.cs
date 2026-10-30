using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.AIChannel.Queries;
using MoAI.AIChannel.Queries.Responses;
using MoAI.Database;

namespace MoAI.AIChannel.Queries;

/// <summary>
/// <inheritdoc cref="QueryTeamAvailableModelsCommand"/>
/// </summary>
public class QueryTeamAvailableModelsCommandHandler : IRequestHandler<QueryTeamAvailableModelsCommand, QueryTeamAvailableModelsCommandResponse>
{
    private readonly DatabaseContext _databaseContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="QueryTeamAvailableModelsCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    public QueryTeamAvailableModelsCommandHandler(DatabaseContext databaseContext)
    {
        _databaseContext = databaseContext;
    }

    /// <inheritdoc/>
    public async Task<QueryTeamAvailableModelsCommandResponse> Handle(QueryTeamAvailableModelsCommand request, CancellationToken cancellationToken)
    {
        var teamId = request.TeamId;
        var authorizedModelIds = await _databaseContext.AiModelAuthorizations
            .Where(x => x.TeamId == teamId)
            .Select(x => x.AiModelId)
            .ToListAsync(cancellationToken);

        // 团队可用口径与 TeamModelQueryExtensions 一致：团队自有渠道 ∪ 公开模型 ∪ 已授权模型，模型与渠道均启用；
        // 团队自有渠道排前：列表同名去重时优先保留团队自己的模型.
        var query = from m in _databaseContext.AiModels
                    join c in _databaseContext.AiChannels on m.ChannelId equals c.Id
                    where m.Enabled && c.Enabled && (c.TeamId == teamId || m.IsPublic || authorizedModelIds.Contains(m.Id))
                    orderby c.TeamId == teamId ? 0 : 1, m.ModelId, m.Id
                    select new { m, c };

        var models = await query.ToListAsync(cancellationToken);

        // 同名 model_id 可能同时来自团队自有渠道与系统渠道，按模型名去重保留第一条（团队自有优先）.
        var items = models
            .GroupBy(x => x.m.ModelId, StringComparer.Ordinal)
            .Select(g => g.First())
            .Select(x => new TeamAvailableModelItem
            {
                AiModelId = x.m.Id,
                Name = x.m.Name,
                ModelId = x.m.ModelId,
                ChannelName = x.c.Name,
                ProviderKey = x.c.ProviderKey,
                SupportsVision = x.m.SupportsVision,
                IsTeamChannel = x.c.TeamId == teamId,
            })
            .ToList();

        return new QueryTeamAvailableModelsCommandResponse { Items = items };
    }
}
