using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.AI.Services;
using MoAI.App.Queries;
using MoAI.App.Queries.Responses;
using MoAI.Database;
using MoAI.Database.Aggregates;
using MoAI.Infra.Exceptions;

namespace MoAI.App.Handlers;

/// <summary>
/// <inheritdoc cref="QueryAppSessionContextUsageCommand"/>
/// </summary>
public class QueryAppSessionContextUsageCommandHandler : IRequestHandler<QueryAppSessionContextUsageCommand, QueryAppSessionContextUsageCommandResponse>
{
    private readonly DatabaseContext _databaseContext;
    private readonly IAgentChatUsageReader _usageReader;

    /// <summary>
    /// Initializes a new instance of the <see cref="QueryAppSessionContextUsageCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    /// <param name="usageReader">会话热态用量读取.</param>
    public QueryAppSessionContextUsageCommandHandler(DatabaseContext databaseContext, IAgentChatUsageReader usageReader)
    {
        _databaseContext = databaseContext;
        _usageReader = usageReader;
    }

    /// <inheritdoc/>
    public async Task<QueryAppSessionContextUsageCommandResponse> Handle(QueryAppSessionContextUsageCommand request, CancellationToken cancellationToken)
    {
        var session = await _databaseContext.AppAgentSessions
            .Where(x => x.Id == request.SessionId)
            .Select(x => new { x.Id, x.AppId, x.CreateUserId, x.InputTokens, x.OutTokens, x.TotalTokens, x.CachedTokens, x.ContextTokens })
            .FirstOrDefaultAsync(cancellationToken);

        // 非归属用户按不存在处理，避免泄露会话存在性
        if (session == null || session.CreateUserId != request.ContextUserId)
        {
            throw new BusinessException("会话不存在.") { StatusCode = 404 };
        }

        var response = new QueryAppSessionContextUsageCommandResponse { SessionId = session.Id };

        // 生效模型与正式对话一致：已发布应用取发布快照，否则取实时草稿；流程应用无 Agent 配置行时上限未知
        var app = await _databaseContext.Apps
            .FirstOrDefaultAsync(x => x.Id == session.AppId, cancellationToken);
        var config = app == null
            ? null
            : await _databaseContext.AppAgentConfigs
                .FirstOrDefaultAsync(x => x.AppId == session.AppId, cancellationToken);
        if (app != null && config != null)
        {
            var effectiveConfig = AppAgentConfigSnapshot.ResolveEffectiveConfig(app, config, preferPublished: true);
            if (effectiveConfig.ModelId != Guid.Empty)
            {
                var model = await _databaseContext.AiModels
                    .Where(x => x.Id == effectiveConfig.ModelId)
                    .Select(x => new { x.Name, x.ContextWindow })
                    .FirstOrDefaultAsync(cancellationToken);
                if (model != null)
                {
                    response.ModelName = model.Name;
                    response.ContextWindow = Math.Max(0, model.ContextWindow);
                }
            }
        }

        // 热态是流式期间的最新累计（落库只在流结束后 flush 一次）；热态过期或为空时回退落库值
        var hotUsage = await _usageReader.GetUsageAsync(session.Id, cancellationToken);
        if (hotUsage.TotalTokens > 0)
        {
            response.InputTokens = hotUsage.InputTokens;
            response.OutTokens = hotUsage.OutTokens;
            response.TotalTokens = hotUsage.TotalTokens;
            response.CachedTokens = hotUsage.CachedTokens;
            response.ContextTokens = hotUsage.LastPromptTokens;
        }
        else
        {
            response.InputTokens = session.InputTokens;
            response.OutTokens = session.OutTokens;
            response.TotalTokens = session.TotalTokens;
            response.CachedTokens = session.CachedTokens;
            response.ContextTokens = session.ContextTokens;
        }

        return response;
    }
}
