using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.Database;
using MoAI.Database.Enums;
using MoAI.Infra.Exceptions;
using MoAI.Timer.Queries;
using MoAI.Timer.Queries.Responses;
using MoAI.Timer.Services;

namespace MoAI.Timer.Handlers;

/// <summary>
/// <inheritdoc cref="QueryTeamTimerExecutionsCommand"/>
/// </summary>
public class QueryTeamTimerExecutionsCommandHandler : IRequestHandler<QueryTeamTimerExecutionsCommand, QueryTeamTimerExecutionsCommandResponse>
{
    private readonly DatabaseContext _databaseContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="QueryTeamTimerExecutionsCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    public QueryTeamTimerExecutionsCommandHandler(DatabaseContext databaseContext)
    {
        _databaseContext = databaseContext;
    }

    /// <inheritdoc/>
    public async Task<QueryTeamTimerExecutionsCommandResponse> Handle(QueryTeamTimerExecutionsCommand request, CancellationToken cancellationToken)
    {
        // 定时器被软删除后历史仍可查看：IgnoreQueryFilters 取回（含已删除）以完成团队归属校验
        var timer = await _databaseContext.TeamTimers
            .IgnoreQueryFilters()
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == request.TimerId, cancellationToken)
            ?? throw new BusinessException("定时器不存在.") { StatusCode = 404 };

        var query = _databaseContext.TeamTimerExecutions.AsNoTracking()
            .Where(x => x.TimerId == request.TimerId);

        var totalCount = await query.CountAsync(cancellationToken);

        var executions = await query
            .OrderByDescending(x => x.CreateTime)
            .Skip((request.PageNo - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync(cancellationToken);

        // 触发人名字批量解析（调度触发为定时器创建人，手动触发为操作人）
        var userIds = executions.Select(x => x.TriggerUserId).Where(x => x > 0).Distinct().ToList();
        var userNames = await _databaseContext.Users.AsNoTracking()
            .Where(x => userIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, x => string.IsNullOrWhiteSpace(x.NickName) ? x.UserName : x.NickName, cancellationToken);

        var items = executions.Select(execution => new TeamTimerExecutionItem
        {
            ExecutionId = execution.Id,
            TimerId = execution.TimerId,
            TriggerType = (TimerTriggerType)execution.TriggerType,
            TriggerUserId = execution.TriggerUserId,
            TriggerUserName = userNames.TryGetValue(execution.TriggerUserId, out var name) ? name : string.Empty,
            Status = (TimerExecutionStatus)execution.Status,
            AttemptCount = execution.AttemptCount,
            StartTime = execution.StartTime,
            EndTime = execution.EndTime,
            DurationMs = execution.DurationMs,
            NextRetryTime = execution.NextRetryTime,
            AppInstanceId = execution.AppInstanceId,
            SessionId = execution.SessionId,
            Result = execution.Result,
            ErrorMessage = execution.ErrorMessage,
            CreateTime = execution.CreateTime,
            CreateUserId = (int)execution.TriggerUserId,
            UpdateTime = execution.UpdateTime,
            UpdateUserId = (int)execution.UpdateUserId,
        }).ToList();

        return new QueryTeamTimerExecutionsCommandResponse
        {
            TotalCount = totalCount,
            Items = items
        };
    }
}
