using Microsoft.EntityFrameworkCore;
using MoAI.App.Queries.Responses;
using MoAI.Database;

namespace MoAI.App.Services;

/// <summary>
/// 应用列表项补充信息：批量填充所属团队名称与使用次数（累计会话数，与用量监控会话数同口径）.
/// </summary>
internal static class AppListItemHelper
{
    /// <summary>
    /// 批量填充列表项的团队名称与使用次数；查不到团队（已解散等）时团队名称为空串.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    /// <param name="items">待填充的应用列表项.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    public static async Task FillTeamAndUsageAsync(DatabaseContext databaseContext, IReadOnlyList<AppItem> items, CancellationToken cancellationToken)
    {
        if (items.Count == 0)
        {
            return;
        }

        var teamIds = items.Select(x => (int)x.TeamId).Distinct().ToList();
        var teamNames = await databaseContext.Teams
            .AsNoTracking()
            .Where(x => teamIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, x => x.Name, cancellationToken);

        var appIds = items.Select(x => x.AppId).ToList();
        var useCounts = await databaseContext.AppAgentSessions
            .AsNoTracking()
            .Where(x => appIds.Contains(x.AppId))
            .GroupBy(x => x.AppId)
            .Select(g => new { g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Count, cancellationToken);

        foreach (var item in items)
        {
            item.TeamName = teamNames.TryGetValue((int)item.TeamId, out var teamName) ? teamName : string.Empty;
            item.UseCount = useCounts.TryGetValue(item.AppId, out var useCount) ? useCount : 0;
        }
    }
}
