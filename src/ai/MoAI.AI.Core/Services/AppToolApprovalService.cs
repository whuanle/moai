using Maomi;
using MoAI.AI;
using StackExchange.Redis.Extensions.Core.Abstractions;

namespace MoAI.AI.Services;

/// <summary>
/// 工具审批闸口的会话上下文：审批记录归属信息，随 SSE 请求头解析得到.
/// </summary>
public sealed class AppToolApprovalGate
{
    /// <summary>
    /// 审批模式（auto/approval）.
    /// </summary>
    public required string Mode { get; init; }

    /// <summary>
    /// 应用 id.
    /// </summary>
    public required Guid AppId { get; init; }

    /// <summary>
    /// 会话 id.
    /// </summary>
    public required Guid SessionId { get; init; }

    /// <summary>
    /// 会话归属用户 id.
    /// </summary>
    public required long UserId { get; init; }

    /// <summary>
    /// 审批策略（应用配置 execution_settings.toolApproval 节）：
    /// 审批模式下白名单插件与沙箱自动放行，无需挂起等待人工决策.
    /// </summary>
    public AppToolApprovalPolicy Policy { get; init; } = AppToolApprovalPolicy.Empty;
}

/// <summary>
/// 工具人工审批服务：在工具执行前创建 Redis 审批记录并轮询等待决策接口改写状态，
/// 前端审批卡通过 POST /app/session/{id}/tool-approval 落决策.
/// </summary>
[InjectOnScoped]
public sealed class AppToolApprovalService
{
    private readonly IRedisDatabase _redisDatabase;

    /// <summary>
    /// Initializes a new instance of the <see cref="AppToolApprovalService"/> class.
    /// </summary>
    /// <param name="redisDatabase">Redis 数据库.</param>
    public AppToolApprovalService(IRedisDatabase redisDatabase)
    {
        _redisDatabase = redisDatabase;
    }

    /// <summary>
    /// 创建待审批记录并阻塞等待人工决策（轮询 Redis）.
    /// </summary>
    /// <param name="gate">会话审批上下文.</param>
    /// <param name="toolName">真实工具名.</param>
    /// <param name="toolTitle">工具展示标题.</param>
    /// <param name="argsJson">工具参数 JSON 文本.</param>
    /// <returns>最终状态：approved/rejected/timeout（异常不影响等待，按 timeout 处理）.</returns>
    public async Task<string> WaitDecisionAsync(AppToolApprovalGate gate, string toolName, string toolTitle, string? argsJson)
    {
        var record = new AppToolApprovalRecord
        {
            Id = Guid.CreateVersion7(),
            SessionId = gate.SessionId,
            AppId = gate.AppId,
            UserId = gate.UserId,
            ToolName = toolName,
            ToolTitle = toolTitle,
            ArgsJson = argsJson ?? string.Empty,
            Status = AppToolApprovalContract.StatusPending,
            CreatedAt = DateTimeOffset.Now,
        };

        var recordKey = AppToolApprovalContract.RecordKey(record.Id);
        var pendingKey = AppToolApprovalContract.PendingIndexKey(gate.SessionId);
        var ttl = TimeSpan.FromSeconds(AppToolApprovalContract.TimeoutSeconds + 60);

        try
        {
            await _redisDatabase.Database.StringSetAsync(recordKey, record.ToRedisValue(), ttl);
            await _redisDatabase.Database.HashSetAsync(pendingKey, record.Id.ToString("N"), toolName);
            await _redisDatabase.Database.KeyExpireAsync(pendingKey, ttl);
        }
#pragma warning disable CA1031 // 写入失败时按超时返回，工具不执行
        catch
        {
            return AppToolApprovalContract.StatusTimeout;
        }
#pragma warning restore CA1031

        var deadline = DateTimeOffset.UtcNow.AddSeconds(AppToolApprovalContract.TimeoutSeconds);
        while (DateTimeOffset.UtcNow < deadline)
        {
            await Task.Delay(AppToolApprovalContract.PollIntervalMilliseconds).ConfigureAwait(false);

            AppToolApprovalRecord? current = null;
            try
            {
                current = await _redisDatabase.GetAsync<AppToolApprovalRecord>(recordKey);
            }
#pragma warning disable CA1031 // 读失败视为仍在等待，下一轮重试
            catch
            {
                continue;
            }
#pragma warning restore CA1031

            if (current == null)
            {
                break;
            }

            if (current.Status != AppToolApprovalContract.StatusPending)
            {
                await CleanupIndexAsync(pendingKey, record.Id).ConfigureAwait(false);
                return current.Status;
            }
        }

        // 超时：回写 timeout 状态并清理索引，避免决策接口命中已放弃的等待
        record.Status = AppToolApprovalContract.StatusTimeout;
        try
        {
            await _redisDatabase.Database.StringSetAsync(recordKey, record.ToRedisValue(), TimeSpan.FromMinutes(2));
            await CleanupIndexAsync(pendingKey, record.Id).ConfigureAwait(false);
        }
#pragma warning disable CA1031 // 清理失败不影响超时结果返回
        catch
        {
            // 记录带 TTL 会自动过期
        }
#pragma warning restore CA1031

        return AppToolApprovalContract.StatusTimeout;
    }

    private async Task CleanupIndexAsync(string pendingKey, Guid approvalId)
    {
        try
        {
            await _redisDatabase.Database.HashDeleteAsync(pendingKey, approvalId.ToString("N")).ConfigureAwait(false);
        }
#pragma warning disable CA1031 // 索引清理失败不影响决策结果，记录/索引均有 TTL
        catch
        {
            // 忽略
        }
#pragma warning restore CA1031
    }
}
