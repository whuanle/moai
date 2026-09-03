using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.Account.Services;
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
    private readonly IUserInfoFillService _userInfoFillService;

    /// <summary>
    /// Initializes a new instance of the <see cref="QueryAIChannelListCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    /// <param name="userInfoFillService">用户信息填充服务.</param>
    public QueryAIChannelListCommandHandler(DatabaseContext databaseContext, IUserInfoFillService userInfoFillService)
    {
        _databaseContext = databaseContext;
        _userInfoFillService = userInfoFillService;
    }

    /// <inheritdoc/>
    public async Task<QueryAIChannelListCommandResponse> Handle(QueryAIChannelListCommand request, CancellationToken cancellationToken)
    {
        var items = await _databaseContext.AiChannels
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

        await _userInfoFillService.FillAsync(items, cancellationToken);

        return new QueryAIChannelListCommandResponse { Items = items };
    }
}
