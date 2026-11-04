using Microsoft.EntityFrameworkCore;
using MoAI.Database;
using MoAI.Database.Entities;
using MoAI.Database.Enums;

namespace MoAI.App.Services;

/// <summary>
/// 团队资源 key 生成/复用器：资源（团队应用、知识库、知识图谱）开启对外功能时调用——
/// 先查库有无记录，无则写入、有则复用（一个资源一把 key，应用 ACP/A2A 共用其应用的 key）。
/// 仅应在资源功能开关校验通过后调用（未开启功能不生成 key）。
/// </summary>
public static class TeamResourceKeyEnsurer
{
    /// <summary>
    /// 定位（必要时创建）资源的团队资源 key.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    /// <param name="teamId">归属团队 id.</param>
    /// <param name="resourceType">资源类型.</param>
    /// <param name="resourceId">资源 id（字符串形态：app 行=应用 Guid、wiki 行=wikiId、kg 行=kgId）.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>返回 (资源 key 行, 是否本次新建).</returns>
    public static async Task<(TeamResourceKeyEntity Entity, bool Created)> EnsureAsync(
        DatabaseContext databaseContext, int teamId, TeamResourceType resourceType, string resourceId, CancellationToken cancellationToken = default)
    {
        var existing = await databaseContext.TeamResourceKeys
            .FirstOrDefaultAsync(x => x.TeamId == teamId && x.ResourceType == resourceType && x.ResourceId == resourceId, cancellationToken);
        if (existing != null)
        {
            return (existing, false);
        }

        var (key, keyPrefix) = TeamResourceKeyGenerator.New();
        var entity = new TeamResourceKeyEntity
        {
            Id = Guid.CreateVersion7(),
            TeamId = teamId,
            ResourceType = resourceType,
            ResourceId = resourceId,
            Key = key,
            KeyPrefix = keyPrefix,
            Status = TeamResourceKeyStatus.Enabled,
        };

        databaseContext.TeamResourceKeys.Add(entity);
        await databaseContext.SaveChangesAsync(cancellationToken);
        return (entity, true);
    }
}
