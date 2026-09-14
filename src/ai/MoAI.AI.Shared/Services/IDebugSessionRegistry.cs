using MoAI.AI.Models;

namespace MoAI.AI.Services;

/// <summary>
/// 调试会话注册表（Redis）：仅存调试会话的应用/用户归属，供对话运行时在无正式会话行时回落解析；
/// 不产生任何业务表记录，读取时滑动续期.
/// </summary>
public interface IDebugSessionRegistry
{
    /// <summary>
    /// 创建调试会话注册项.
    /// </summary>
    /// <param name="sessionId">调试会话 id.</param>
    /// <param name="appId">应用 id.</param>
    /// <param name="teamId">团队 id.</param>
    /// <param name="userId">发起调试的用户 id.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>异步任务.</returns>
    Task CreateAsync(Guid sessionId, Guid appId, int teamId, long userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 读取调试会话注册项，并滑动续期 TTL.
    /// </summary>
    /// <param name="sessionId">调试会话 id.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>注册项；不存在返回 null.</returns>
    Task<DebugSessionRegistryEntry?> GetAsync(Guid sessionId, CancellationToken cancellationToken = default);
}
