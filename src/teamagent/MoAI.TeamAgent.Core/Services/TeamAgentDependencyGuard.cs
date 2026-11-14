using Microsoft.EntityFrameworkCore;
using MoAI.Database;
using MoAI.Infra.Exceptions;
using MoAI.KnowledgeGraph.Models;
using MoAI.TeamAgent.Models;

namespace MoAI.TeamAgent.Services;

/// <summary>
/// 团队智能体依赖校验：与应用配置绑定同口径（团队自有或系统公开/已授权插件、本团队知识库、团队可用模型）.
/// </summary>
public static class TeamAgentDependencyGuard
{
    /// <summary>
    /// 校验插件依赖：仅允许团队自有，或系统插件中已公开/已授权本团队的；去重后返回归一列表.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    /// <param name="teamId">团队 id.</param>
    /// <param name="pluginIds">插件 id 集合.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>归一后的插件 id 列表.</returns>
    public static async Task<List<Guid>> EnsurePluginIdsAsync(DatabaseContext databaseContext, long teamId, IEnumerable<Guid>? pluginIds, CancellationToken cancellationToken)
    {
        var ids = pluginIds?.Where(x => x != Guid.Empty).Distinct().ToList() ?? new List<Guid>();
        if (ids.Count == 0)
        {
            return ids;
        }

        var rows = await databaseContext.Plugins
            .Where(x => ids.Contains(x.Id))
            .Select(x => new { x.Id, x.TeamId, x.IsSystem, x.IsPublic })
            .ToListAsync(cancellationToken);

        var authorized = await databaseContext.PluginTeamAuthorizations
            .Where(x => x.TeamId == (int)teamId)
            .Select(x => x.PluginId)
            .ToListAsync(cancellationToken);

        var authorizedSet = authorized.ToHashSet();
        var rowMap = rows.ToDictionary(x => x.Id);
        var invalid = new List<Guid>();

        foreach (var id in ids)
        {
            if (!rowMap.TryGetValue(id, out var row))
            {
                invalid.Add(id);
                continue;
            }

            // 团队自有插件
            if (row.TeamId == (int)teamId)
            {
                continue;
            }

            // 系统插件：公开或已授权本团队
            var accessible = row.IsSystem && row.TeamId == 0 && (row.IsPublic || authorizedSet.Contains(id));
            if (!accessible)
            {
                invalid.Add(id);
            }
        }

        if (invalid.Count > 0)
        {
            throw new BusinessException("包含该团队无权使用的插件，请重新选择.") { StatusCode = 400 };
        }

        return ids;
    }

    /// <summary>
    /// 校验知识库依赖：仅允许绑定本团队的知识库；去重后返回归一列表.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    /// <param name="teamId">团队 id.</param>
    /// <param name="wikiIds">知识库 id 集合.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>归一后的知识库 id 列表.</returns>
    public static async Task<List<long>> EnsureWikiIdsAsync(DatabaseContext databaseContext, long teamId, IEnumerable<long>? wikiIds, CancellationToken cancellationToken)
    {
        var ids = wikiIds?.Where(x => x > 0).Distinct().ToList() ?? new List<long>();
        if (ids.Count == 0)
        {
            return ids;
        }

        var owned = await databaseContext.Wikis
            .Where(x => x.TeamId == (int)teamId)
            .Select(x => (long)x.Id)
            .ToListAsync(cancellationToken);

        var ownedSet = owned.ToHashSet();
        var invalid = ids.Where(x => !ownedSet.Contains(x)).ToList();

        if (invalid.Count > 0)
        {
            throw new BusinessException("包含不属于该团队的知识库，请重新选择.") { StatusCode = 400 };
        }

        return ids;
    }

    /// <summary>
    /// 校验知识图谱依赖：仅允许绑定本团队的平台托管图（接入图不支持应用侧检索绑定）；去重后返回归一列表.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    /// <param name="teamId">团队 id.</param>
    /// <param name="graphIds">知识图谱 id 集合.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>归一后的知识图谱 id 列表.</returns>
    public static async Task<List<long>> EnsureGraphIdsAsync(DatabaseContext databaseContext, long teamId, IEnumerable<long>? graphIds, CancellationToken cancellationToken)
    {
        var ids = graphIds?.Where(x => x > 0).Distinct().ToList() ?? new List<long>();
        if (ids.Count == 0)
        {
            return ids;
        }

        var owned = await databaseContext.KnowledgeGraphs
            .Where(x => x.TeamId == (int)teamId && x.Mode == KnowledgeGraphModes.Managed)
            .Select(x => x.Id)
            .ToListAsync(cancellationToken);

        var ownedSet = owned.ToHashSet();
        var invalid = ids.Where(x => !ownedSet.Contains(x)).ToList();

        if (invalid.Count > 0)
        {
            throw new BusinessException("包含不属于该团队或不支持检索的知识图谱，请重新选择.") { StatusCode = 400 };
        }

        return ids;
    }

    /// <summary>
    /// 校验子智能体标识 key 在智能体内唯一.
    /// </summary>
    /// <param name="subs">子智能体列表.</param>
    public static void EnsureSubKeysUnique(IReadOnlyList<TeamAgentSubItem> subs)
    {
        var duplicated = subs
            .GroupBy(x => x.Key, StringComparer.Ordinal)
            .Where(x => x.Count() > 1)
            .Select(x => x.Key)
            .ToList();

        if (duplicated.Count > 0)
        {
            throw new BusinessException($"子智能体标识重复：{string.Join("、", duplicated)}.") { StatusCode = 400 };
        }
    }
}
