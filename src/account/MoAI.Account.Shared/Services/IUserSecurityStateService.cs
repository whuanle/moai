namespace MoAI.Account.Services;

/// <summary>
/// 用户安全态（禁用/删除/鉴权版本）分布式缓存：全实例共享 Redis，无进程内缓存；
/// 写入恒为 DB 提交后的写穿透，读取未命中回库，Lua 单调版本保护避免并发读回填旧值.
/// </summary>
public interface IUserSecurityStateService
{
    /// <summary>
    /// 读取用户安全态（Redis 命中直接返回，未命中回库并回填）.
    /// </summary>
    /// <param name="userId">用户 id.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>用户安全态；用户不存在时返回 <see cref="UserSecurityState.IsDeleted"/>/<see cref="UserSecurityState.IsDisable"/> 均为 true.</returns>
    Task<UserSecurityState> GetAsync(long userId, CancellationToken cancellationToken);

    /// <summary>
    /// 写穿透用户安全态：DB 提交后调用，覆盖全实例可见的安全态（版本只增不减）.
    /// </summary>
    /// <param name="userId">用户 id.</param>
    /// <param name="isDisable">是否禁用.</param>
    /// <param name="isDeleted">是否已删除.</param>
    /// <param name="authVersion">鉴权版本.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>Task.</returns>
    Task SetAsync(long userId, bool isDisable, bool isDeleted, long authVersion, CancellationToken cancellationToken);
}

/// <summary>
/// 用户安全态：鉴权中间件与外部 token 校验只依赖这三个字段，不读用户资料缓存.
/// </summary>
public sealed class UserSecurityState
{
    /// <summary>
    /// 用户 id.
    /// </summary>
    public long UserId { get; init; }

    /// <summary>
    /// 是否禁用.
    /// </summary>
    public bool IsDisable { get; init; }

    /// <summary>
    /// 是否已删除（或不存在）.
    /// </summary>
    public bool IsDeleted { get; init; }

    /// <summary>
    /// 当前鉴权版本.
    /// </summary>
    public long AuthVersion { get; init; }
}
