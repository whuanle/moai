using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.AIChannel.Queries;
using MoAI.AIChannel.Queries.Responses;
using MoAI.Database;
using MoAI.Infra.Exceptions;

namespace MoAI.AIChannel.Queries;

/// <summary>
/// <inheritdoc cref="QueryAIChannelAuthorizationCommand"/>
/// </summary>
public class QueryAIChannelAuthorizationCommandHandler : IRequestHandler<QueryAIChannelAuthorizationCommand, QueryAIChannelAuthorizationCommandResponse>
{
    private readonly DatabaseContext _databaseContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="QueryAIChannelAuthorizationCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext"></param>
    public QueryAIChannelAuthorizationCommandHandler(DatabaseContext databaseContext)
    {
        _databaseContext = databaseContext;
    }

    /// <inheritdoc/>
    public async Task<QueryAIChannelAuthorizationCommandResponse> Handle(QueryAIChannelAuthorizationCommand request, CancellationToken cancellationToken)
    {
        var channel = await _databaseContext.AiChannels
            .FirstOrDefaultAsync(x => x.Id == request.ChannelId && x.IsDeleted == 0, cancellationToken);

        if (channel == null)
        {
            throw new BusinessException("未找到渠道，请检查 id 是否正确.") { StatusCode = 404 };
        }

        var models = await _databaseContext.AiModels
            .Where(x => x.ChannelId == request.ChannelId && x.IsDeleted == 0)
            .Select(x => new { x.Id, x.IsPublic })
            .ToListAsync(cancellationToken);

        var privateModelIds = models.Where(x => !x.IsPublic).Select(x => x.Id).ToList();

        var items = await _databaseContext.AiModelAuthorizations
            .Where(x => privateModelIds.Contains(x.AiModelId))
            .Join(_databaseContext.Teams, a => a.TeamId, t => t.Id, (a, t) => new { a.TeamId, t.Name })
            .GroupBy(x => new { x.TeamId, x.Name })
            .Select(g => new { g.Key.TeamId, g.Key.Name })
            .OrderBy(x => x.TeamId)
            .ToListAsync(cancellationToken);

        return new QueryAIChannelAuthorizationCommandResponse
        {
            ChannelId = request.ChannelId,
            ModelCount = models.Count,
            PrivateModelCount = privateModelIds.Count,
            PublicModelCount = models.Count - privateModelIds.Count,
            Items = items
                .Select(x => new QueryAIModelAuthorizationCommandResponseItem
                {
                    TeamId = x.TeamId,
                    TeamName = x.Name,
                })
                .ToList(),
        };
    }
}
