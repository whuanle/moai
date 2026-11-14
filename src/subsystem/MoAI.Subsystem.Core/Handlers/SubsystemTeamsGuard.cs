using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.Database;
using MoAI.Infra.Models;
using MoAI.Subsystem.Queries.Responses;

namespace MoAI.Subsystem.Handlers;

/// <summary>
/// 子系统团队授权公共校验：指定团队范围下校验团队存在并去重.
/// </summary>
internal static class SubsystemTeamsGuard
{
    /// <summary>
    /// 校验团队 id 列表全部存在并去重返回；全员范围返回空列表.
    /// </summary>
    /// <param name="teamScope">团队授权范围.</param>
    /// <param name="teamIds">授权团队 id 列表.</param>
    /// <param name="databaseContext">数据库上下文.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>返回去重后的团队 id 列表.</returns>
    public static async Task<List<int>> NormalizeAsync(Database.Enums.SubsystemTeamScope teamScope, IReadOnlyList<int> teamIds, DatabaseContext databaseContext, CancellationToken cancellationToken)
    {
        if (teamScope != Database.Enums.SubsystemTeamScope.SpecifiedTeams)
        {
            return [];
        }

        var distinctIds = teamIds.Distinct().ToList();
        var foundIds = await databaseContext.Teams
            .Where(x => distinctIds.Contains(x.Id))
            .Select(x => x.Id)
            .ToListAsync(cancellationToken);
        var missingIds = distinctIds.Except(foundIds).ToList();
        if (missingIds.Count > 0)
        {
            throw new Infra.Exceptions.BusinessException($"团队不存在: {string.Join(", ", missingIds)}") { StatusCode = 400 };
        }

        return distinctIds;
    }
}
