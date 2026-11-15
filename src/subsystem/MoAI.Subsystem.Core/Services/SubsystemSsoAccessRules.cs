using Microsoft.EntityFrameworkCore;
using MoAI.Database;
using MoAI.Database.Entities;
using MoAI.Database.Enums;
using MoAI.Org.Services;

namespace MoAI.Subsystem.Core.Services;

/// <summary>
/// 委托 SSO 用户授权判定（团队维度 ∪ 组织维度）：SSO 预检与委托码签发共用，避免两处口径漂移.
/// </summary>
internal static class SubsystemSsoAccessRules
{
    /// <summary>
    /// 判定用户可否使用指定子系统：团队范围命中或组织授权命中任一即放通.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    /// <param name="orgGrantService">统一组织授权领域服务.</param>
    /// <param name="subsystem">子系统接入行.</param>
    /// <param name="userId">站内用户 id.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>是否获授权.</returns>
    public static async Task<bool> IsUserAllowedAsync(
        DatabaseContext databaseContext,
        IOrgGrantService orgGrantService,
        AccessAppEntity subsystem,
        long userId,
        CancellationToken cancellationToken)
    {
        var userTeamIds = await databaseContext.TeamUsers
            .Where(x => x.UserId == userId)
            .Select(x => x.TeamId)
            .ToListAsync(cancellationToken);

        bool teamHit;
        if (subsystem.TeamScope == SubsystemTeamScope.SpecifiedTeams)
        {
            var authorizedTeamIds = await databaseContext.SubsystemTeamAuths
                .Where(x => x.SubsystemId == subsystem.Id)
                .Select(x => x.TeamId)
                .ToListAsync(cancellationToken);
            teamHit = userTeamIds.Intersect(authorizedTeamIds).Any();
        }
        else
        {
            // 全员范围要求用户至少加入一个团队（无团队且无组织授权不放行）
            teamHit = userTeamIds.Count > 0;
        }

        if (teamHit)
        {
            return true;
        }

        // 组织维度：统一组织授权表（org_grant，资源类型 subsystem）按角色方向规则（self/up/down）判定
        return await orgGrantService.IsAllowedAsync(OrgGrantResourceType.Subsystem, subsystem.Id, userId, cancellationToken);
    }
}
