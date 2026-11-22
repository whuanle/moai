using Mediator;
using MoAI.Infra.Exceptions;
using MoAI.Subsystem.Models;
using MoAI.Subsystem.Queries;
using MoAI.Subsystem.Queries.Responses;
using MoAI.Subsystem.Services;

namespace MoAI.Subsystem.Handlers;

/// <summary>
/// <inheritdoc cref="QuerySubsysEventsCommand"/>
/// </summary>
public class QuerySubsysEventsCommandHandler : IRequestHandler<QuerySubsysEventsCommand, QuerySubsysEventsCommandResponse>
{
    private readonly AccessEventStore _eventStore;

    /// <summary>
    /// Initializes a new instance of the <see cref="QuerySubsysEventsCommandHandler"/> class.
    /// </summary>
    /// <param name="eventStore">事件补发索引.</param>
    public QuerySubsysEventsCommandHandler(AccessEventStore eventStore)
    {
        _eventStore = eventStore;
    }

    /// <inheritdoc/>
    public async ValueTask<QuerySubsysEventsCommandResponse> Handle(QuerySubsysEventsCommand request, CancellationToken cancellationToken)
    {
        // 机器面 Action 为简化绑定在内部构造命令，自动化校验不会覆盖该路径，这里统一守卫参数语义
        if (request.SubsystemId == Guid.Empty)
        {
            throw new BusinessException("接入 id 不正确.") { StatusCode = 400 };
        }

        if (request.Limit is < 1 or > 500)
        {
            throw new BusinessException("limit 取值范围 1~500.") { StatusCode = 400 };
        }

        var eventName = string.IsNullOrWhiteSpace(request.Event) ? null : request.Event.Trim();
        var eventPrefix = string.IsNullOrWhiteSpace(request.EventPrefix) ? null : request.EventPrefix.Trim();
        if (eventName is not null && eventPrefix is not null)
        {
            throw new BusinessException("event 与 eventPrefix 不能同时指定.") { StatusCode = 400 };
        }

        if (eventName is { Length: > 128 } || (eventName is not null && !eventName.StartsWith("subsys.", StringComparison.Ordinal)))
        {
            throw new BusinessException("event 必须以 subsys. 开头且最长 128 字符.") { StatusCode = 400 };
        }

        if (eventPrefix is { Length: > 64 } || (eventPrefix is not null && !eventPrefix.StartsWith("subsys.", StringComparison.Ordinal)))
        {
            throw new BusinessException("eventPrefix 必须以 subsys. 开头且最长 64 字符.") { StatusCode = 400 };
        }

        var result = await _eventStore.ReadAsync(
            request.SubsystemId,
            request.AfterId,
            request.Limit,
            eventName,
            eventPrefix,
            cancellationToken);

        return new QuerySubsysEventsCommandResponse
        {
            Items = result.Items,
            HasMore = result.HasMore,
            ResyncRequired = result.ResyncRequired,
            OldestId = result.OldestId,
            NextCursor = result.NextCursor,
            SchemaVersion = SubsystemEventContract.SchemaVersion,
            RetentionDays = SubsystemEventContract.RetentionDays,
        };
    }
}
