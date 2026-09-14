using Microsoft.EntityFrameworkCore;
using MoAI.Database;

namespace MoAI.App.Handlers;

/// <summary>
/// 应用接入授权应用校验：必须属于本团队且为外部应用.
/// </summary>
internal static class AccessAppAuthorizedAppsValidator
{
    /// <summary>
    /// 校验授权应用列表，越权/非法时抛 400.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    /// <param name="teamId">团队 id.</param>
    /// <param name="appIds">授权应用 id 列表.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    public static async Task ValidateAsync(DatabaseContext databaseContext, int teamId, IReadOnlyList<Guid> appIds, CancellationToken cancellationToken)
    {
        if (appIds == null || appIds.Count == 0)
        {
            return;
        }

        var distinct = appIds.Distinct().ToList();
        var validIds = await databaseContext.Apps
            .Where(x => x.TeamId == teamId && x.IsExternal && distinct.Contains(x.Id))
            .Select(x => x.Id)
            .ToListAsync(cancellationToken);

        if (validIds.Count != distinct.Count)
        {
            throw new MoAI.Infra.Exceptions.BusinessException("授权列表包含不属于本团队或非外部的应用.") { StatusCode = 400 };
        }
    }
}
