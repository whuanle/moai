using MoAI.Database.Enums;

namespace MoAI.Team.Services;

/// <summary>
/// 团队领域服务，提供成员角色等事实查询（携带 Redis 缓存）.
/// </summary>
public interface ITeamService
{
    /// <summary>
    /// 查询用户在团队中的角色，不在团队中时返回 null.
    /// </summary>
    /// <param name="teamId">团队 id.</param>
    /// <param name="userId">用户 id.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>返回 <see cref="TeamRole"/>，非成员为 null.</returns>
    Task<TeamRole?> GetMyRoleAsync(long teamId, long userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 移除用户在团队中的角色缓存，在团队成员关系/角色发生变更后调用.
    /// </summary>
    /// <param name="teamId">团队 id.</param>
    /// <param name="userId">用户 id.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns></returns>
    Task RemoveRoleCacheAsync(long teamId, long userId, CancellationToken cancellationToken = default);
}
