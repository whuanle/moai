using MoAI.Account.Services;
using MoAI.Database.Entities;
using MoAI.Database.Enums;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Models;
using MoAI.Team.Services;

namespace MoAI.Skill.Services;

/// <summary>
/// 技能访问控制：按技能归属（平台内置/团队/个人）校验当前用户的管理权限.
/// </summary>
public static class SkillAccessGuard
{
    /// <summary>
    /// 校验用户可创建指定团队范围的技能：个人技能登录即可，团队技能需团队管理员，平台管理员不受限.
    /// </summary>
    public static async Task EnsureCanCreateAsync(int teamId, long userId, IUserAccountService userAccountService, ITeamService teamService, CancellationToken cancellationToken)
    {
        if (teamId == 0)
        {
            return;
        }

        if (await IsPlatformAdminAsync(userId, userAccountService, cancellationToken).ConfigureAwait(false))
        {
            return;
        }

        var role = await teamService.GetMyRoleAsync(teamId, userId, cancellationToken).ConfigureAwait(false);
        if (role is not (TeamRole.Admin or TeamRole.Owner))
        {
            throw new BusinessException("只有团队管理员可以创建团队技能.") { StatusCode = 403 };
        }
    }

    /// <summary>
    /// 校验用户可管理（更新/删除/启停）该技能：平台管理员、团队技能的团队管理员或个人技能归属人.
    /// </summary>
    public static async Task EnsureCanManageAsync(SkillEntity skill, long userId, IUserAccountService userAccountService, ITeamService teamService, CancellationToken cancellationToken)
    {
        if (await IsPlatformAdminAsync(userId, userAccountService, cancellationToken).ConfigureAwait(false))
        {
            return;
        }

        if (skill.TeamId > 0)
        {
            var role = await teamService.GetMyRoleAsync(skill.TeamId, userId, cancellationToken).ConfigureAwait(false);
            if (role is not (TeamRole.Admin or TeamRole.Owner))
            {
                throw new BusinessException("只有团队管理员可以管理该技能.") { StatusCode = 403 };
            }

            return;
        }

        if (skill.CreateUserId != userId)
        {
            throw new BusinessException("只有技能创建人可以管理该技能.") { StatusCode = 403 };
        }
    }

    private static async Task<bool> IsPlatformAdminAsync(long userId, IUserAccountService userAccountService, CancellationToken cancellationToken)
    {
        var userState = await userAccountService.GetUserStateAsync(userId, cancellationToken).ConfigureAwait(false);
        return userState.IsAdmin;
    }
}
