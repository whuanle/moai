using System;
using System.Threading;
using System.Threading.Tasks;
using Maomi;
using Maomi.MQ;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MoAI.Database;
using MoAI.Memory.Events;
using MoAI.Memory.Services;

namespace MoAI.Memory.Services;

/// <summary>
/// <inheritdoc cref="IMemoryExtractionTrigger"/>
/// 校验会话满足自动提取条件（应用开启记忆 + 自动提取 + 用户未关闭 + 非外部会话）后投递 MQ 任务；
/// 任一条件不满足或投递失败都静默跳过，记忆为尽力而为能力.
/// </summary>
[InjectOnScoped]
public sealed class MemoryExtractionTrigger : IMemoryExtractionTrigger
{
    private readonly DatabaseContext _databaseContext;
    private readonly IMemoryService _memoryService;
    private readonly IMessagePublisher _messagePublisher;
    private readonly ILogger<MemoryExtractionTrigger> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="MemoryExtractionTrigger"/> class.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    /// <param name="memoryService">记忆领域服务.</param>
    /// <param name="messagePublisher">MQ 发布者.</param>
    /// <param name="logger">日志.</param>
    public MemoryExtractionTrigger(
        DatabaseContext databaseContext,
        IMemoryService memoryService,
        IMessagePublisher messagePublisher,
        ILogger<MemoryExtractionTrigger> logger)
    {
        _databaseContext = databaseContext;
        _memoryService = memoryService;
        _messagePublisher = messagePublisher;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<bool> TriggerAsync(Guid sessionId, CancellationToken cancellationToken = default)
    {
        try
        {
            var session = await _databaseContext.AppAgentSessions.AsNoTracking()
                .Where(x => x.Id == sessionId)
                .Select(x => new { x.Id, x.AppId, x.CreateUserId, x.UserType })
                .FirstOrDefaultAsync(cancellationToken);
            if (session == null)
            {
                return false;
            }

            if (session.UserType == (int)MoAI.Infra.Models.UserType.External
                || session.UserType == (int)MoAI.Infra.Models.UserType.ExternalApp)
            {
                return false;
            }

            var resolution = await _memoryService.ResolveAsync(session.AppId, session.CreateUserId, cancellationToken);
            if (resolution is not { IsEnabled: true } || !resolution.Settings.AutoExtract)
            {
                return false;
            }

            await _messagePublisher.AutoPublishAsync(new MemoryExtractMessage { SessionId = sessionId });
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "会话 {SessionId} 记忆提取任务投递失败.", sessionId);
            return false;
        }
    }
}
