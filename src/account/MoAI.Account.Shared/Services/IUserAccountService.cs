using MoAI.Account.Queries.Responses;

namespace MoAI.Account.Services;

/// <summary>
/// 用户账号领域服务，封装用户状态的缓存与查询逻辑.
/// </summary>
public interface IUserAccountService
{
    /// <summary>
    /// 获取用户状态信息，查询时先走缓存，缓存不存在时再查数据库并写入缓存.
    /// </summary>
    /// <param name="userId">用户 id.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>返回 <see cref="UserStateInfo"/>.</returns>
    Task<UserStateInfo> GetUserStateAsync(long userId, CancellationToken cancellationToken);

    /// <summary>
    /// 移除用户状态缓存，在用户账号被修改后调用.
    /// </summary>
    /// <param name="userId">用户 id.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns></returns>
    Task RemoveUserStateAsync(long userId, CancellationToken cancellationToken);
}
