using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.AI;
using MoAI.App.Commands;
using MoAI.Database;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Models;
using StackExchange.Redis.Extensions.Core.Abstractions;

namespace MoAI.App.Handlers;

/// <summary>
/// <inheritdoc cref="DecideAppSessionToolApprovalCommand"/>
/// </summary>
public class DecideAppSessionToolApprovalCommandHandler : IRequestHandler<DecideAppSessionToolApprovalCommand, DecideAppSessionToolApprovalResponse>
{
    private readonly DatabaseContext _databaseContext;
    private readonly IRedisDatabase _redisDatabase;

    /// <summary>
    /// Initializes a new instance of the <see cref="DecideAppSessionToolApprovalCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    /// <param name="redisDatabase">Redis 数据库.</param>
    public DecideAppSessionToolApprovalCommandHandler(DatabaseContext databaseContext, IRedisDatabase redisDatabase)
    {
        _databaseContext = databaseContext;
        _redisDatabase = redisDatabase;
    }

    /// <inheritdoc/>
    public async Task<DecideAppSessionToolApprovalResponse> Handle(DecideAppSessionToolApprovalCommand request, CancellationToken cancellationToken)
    {
        var ownerId = await _databaseContext.AppAgentSessions
            .Where(x => x.Id == request.SessionId)
            .Select(x => (long?)x.CreateUserId)
            .FirstOrDefaultAsync(cancellationToken);

        if (ownerId == null || ownerId != request.ContextUserId)
        {
            throw new BusinessException("会话不存在.") { StatusCode = 404 };
        }

        var target = request.Approved
            ? AppToolApprovalContract.StatusApproved
            : AppToolApprovalContract.StatusRejected;

        var pendingKey = AppToolApprovalContract.PendingIndexKey(request.SessionId);
        var entries = await _redisDatabase.Database.HashGetAllAsync(pendingKey);

        foreach (var entry in entries)
        {
            if (!string.Equals(entry.Value.ToString(), request.ToolName, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var approvalId = entry.Name.ToString();
            if (!Guid.TryParse(approvalId, out var id))
            {
                continue;
            }

            var recordKey = AppToolApprovalContract.RecordKey(id);
            var record = await _redisDatabase.GetAsync<AppToolApprovalRecord>(recordKey);
            if (record == null || record.Status != AppToolApprovalContract.StatusPending)
            {
                // 记录已过期/已被处理：清理索引后继续找下一条
                await _redisDatabase.Database.HashDeleteAsync(pendingKey, entry.Name);
                continue;
            }

            record.Status = target;
            await _redisDatabase.Database.StringSetAsync(recordKey, record.ToRedisValue(), TimeSpan.FromMinutes(10));
            await _redisDatabase.Database.HashDeleteAsync(pendingKey, entry.Name);
            return new DecideAppSessionToolApprovalResponse { Status = target };
        }

        // 无匹配待审批记录：工具已自动执行或审批已被处理
        return new DecideAppSessionToolApprovalResponse { Status = AppToolApprovalContract.StatusMissing };
    }
}
