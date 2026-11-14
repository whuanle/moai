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
            .Where(x => x.TeamId == request.TeamId)
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

        // 渠道累计 tokens：ai_model_token_audit 按 model_id 归到 ai_model.channel_id 聚合（该表由 Hangfire 批量落库，非实时）。
        var tokensByChannel = await _databaseContext.AiModelTokenAudits
            .Join(_databaseContext.AiModels,
                audit => audit.ModelId,
                model => model.Id,
                (audit, model) => new { audit.PromptTokens, audit.CompletionTokens, audit.TotalTokens, model.ChannelId })
            .GroupBy(x => x.ChannelId)
            .Select(g => new
            {
                ChannelId = g.Key,
                PromptTokens = g.Sum(x => (long)x.PromptTokens),
                CompletionTokens = g.Sum(x => (long)x.CompletionTokens),
                TotalTokens = g.Sum(x => (long)x.TotalTokens),
            })
            .ToListAsync(cancellationToken);
        var tokensMap = tokensByChannel.ToDictionary(x => x.ChannelId);

        foreach (var item in items)
        {
            if (tokensMap.TryGetValue(item.Id, out var tokens))
            {
                item.PromptTokens = tokens.PromptTokens;
                item.CompletionTokens = tokens.CompletionTokens;
                item.TotalTokens = tokens.TotalTokens;
            }
        }

        await _userInfoFillService.FillAsync(items, cancellationToken);

        return new QueryAIChannelListCommandResponse { Items = items };
    }
}
