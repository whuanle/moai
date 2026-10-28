using MoAI.AI.Models;

namespace MoAI.AI.Services;

/// <summary>
/// 读取 Agent 会话热态 token 用量（实现为 AppChatHotStore 的 Redis 热态；供跨模块只读查询，不暴露写入口）.
/// </summary>
public interface IAgentChatUsageReader
{
    /// <summary>
    /// 读取会话 token 用量聚合（热态不存在时返回零值）.
    /// </summary>
    /// <param name="sessionId">会话 id.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>用量聚合.</returns>
    Task<AppAgentUsageAggregate> GetUsageAsync(Guid sessionId, CancellationToken cancellationToken = default);
}
