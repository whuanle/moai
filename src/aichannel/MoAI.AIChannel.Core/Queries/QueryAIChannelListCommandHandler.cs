using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.AIChannel.Models;
using MoAI.AIChannel.Queries;
using MoAI.AIChannel.Queries.Responses;
using MoAI.Database;

namespace MoAI.AIChannel.Queries;

/// <summary>
/// <inheritdoc cref="QueryAIChannelListCommand"/>
/// </summary>
public class QueryAIChannelListCommandHandler : IRequestHandler<QueryAIChannelListCommand, QueryAIChannelListCommandResponse>
{
    private readonly DatabaseContext _databaseContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="QueryAIChannelListCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext"></param>
    public QueryAIChannelListCommandHandler(DatabaseContext databaseContext)
    {
        _databaseContext = databaseContext;
    }

    /// <inheritdoc/>
    public async Task<QueryAIChannelListCommandResponse> Handle(QueryAIChannelListCommand request, CancellationToken cancellationToken)
    {
        var items = await _databaseContext.AiChannels
            .Where(x => x.IsDeleted == 0)
            .Select(x => new QueryAIChannelListCommandResponseItem
            {
                Id = x.Id,
                ProviderKey = x.ProviderKey,
                Name = x.Name,
                ProtocolFamily = (AIProtocolFamily)x.ProtocolFamily,
                BaseUrl = x.BaseUrl,
                Enabled = x.Enabled,
                Description = x.Description,
                ModelCount = _databaseContext.AiModels.Count(m => m.ChannelId == x.Id && m.IsDeleted == 0),
                CreateTime = x.CreateTime,
                CreateUserId = (int)x.CreateUserId,
                UpdateTime = x.UpdateTime,
                UpdateUserId = (int)x.UpdateUserId,
            })
            .ToListAsync(cancellationToken);

        return new QueryAIChannelListCommandResponse { Items = items };
    }
}
