using MoAI.Account.Services;
using MoAI.Database.Enums;
using MoAI.Infra.Exceptions;
using MoAI.Team.Services;

namespace MoAI.TeamAgent.Services;

/// <summary>
/// 团队智能体访问控制：仅团队 Admin 及以上可管理，团队成员可查看.
/// </summary>
public static class TeamAgentAccessGuard
{
    /// <summary>
    /// 校验用户可管理（创建/更新/删除/头像/启停）该团队智能体：团队 Admin/Owner，平台管理员放行；非团队成员 404.
    /// </summary>
    /// <returns>当前用户团队角色.</returns>
    public static async Task<TeamRole> EnsureCanManageAsync(long teamId, long userId, IUserAccountService userAccountService, ITeamService teamService, CancellationToken cancellationToken)
    {
        var role = await EnsureTeamMemberAsync(teamId, userId, teamService, cancellationToken).ConfigureAwait(false);

        if (await IsPlatformAdminAsync(userId, userAccountService, cancellationToken).ConfigureAwait(false))
        {
            return role;
        }

        if (role is not (TeamRole.Admin or TeamRole.Owner))
        {
            throw new BusinessException("只有团队管理员可以管理该智能体.") { StatusCode = 403 };
        }

        return role;
    }

    /// <summary>
    /// 校验用户是团队成员（查看列表/详情），非成员按目标不存在处理.
    /// </summary>
    /// <returns>当前用户团队角色.</returns>
    public static async Task<TeamRole> EnsureTeamMemberAsync(long teamId, long userId, ITeamService teamService, CancellationToken cancellationToken)
    {
        var role = await teamService.GetMyRoleAsync(teamId, userId, cancellationToken).ConfigureAwait(false);
        if (role == null)
        {
            throw new BusinessException("团队不存在或你不是团队成员.") { StatusCode = 404 };
        }

        return role.Value;
    }

    private static async Task<bool> IsPlatformAdminAsync(long userId, IUserAccountService userAccountService, CancellationToken cancellationToken)
    {
        var userState = await userAccountService.GetUserStateAsync(userId, cancellationToken).ConfigureAwait(false);
        return userState.IsAdmin;
    }
}
