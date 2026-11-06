using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using MoAI.Database;
using MoAI.Infra.Exceptions;
using MoAI.TeamAgent.Models;
using MoAI.TeamAgent.Services;

namespace MoAI.TeamAgent.Handlers;

/// <summary>
/// 智能体创建/更新共用的依赖校验与实体装配逻辑.
/// </summary>
internal static class TeamAgentCommandHelper
{
    /// <summary>
    /// 校验主智能体与全部子智能体的依赖（插件/知识库/知识图谱），返回归一化后的子智能体列表与主智能体依赖.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    /// <param name="teamId">团队 id.</param>
    /// <param name="mainPluginIds">主智能体插件依赖.</param>
    /// <param name="mainWikiIds">主智能体知识库依赖.</param>
    /// <param name="mainGraphIds">主智能体知识图谱依赖.</param>
    /// <param name="subs">子智能体列表.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>归一后的 (主插件集, 主知识库集, 主图谱集, 子智能体列表).</returns>
    public static async Task<(List<Guid> MainPluginIds, List<long> MainWikiIds, List<long> MainGraphIds, List<TeamAgentSubItem> Subs)> NormalizeAsync(
        DatabaseContext databaseContext,
        long teamId,
        IReadOnlyList<Guid> mainPluginIds,
        IReadOnlyList<long> mainWikiIds,
        IReadOnlyList<long> mainGraphIds,
        IReadOnlyList<TeamAgentSubItem> subs,
        CancellationToken cancellationToken)
    {
        TeamAgentDependencyGuard.EnsureSubKeysUnique(subs);

        // 主智能体与全部子智能体的依赖合并后统一校验，减少查询次数
        var allPluginIds = mainPluginIds.Concat(subs.SelectMany(x => x.PluginIds));
        var allWikiIds = mainWikiIds.Concat(subs.SelectMany(x => x.WikiIds));
        var allGraphIds = mainGraphIds.Concat(subs.SelectMany(x => x.GraphIds));

        var validPluginIds = (await TeamAgentDependencyGuard.EnsurePluginIdsAsync(databaseContext, teamId, allPluginIds, cancellationToken).ConfigureAwait(false)).ToHashSet();
        var validWikiIds = (await TeamAgentDependencyGuard.EnsureWikiIdsAsync(databaseContext, teamId, allWikiIds, cancellationToken).ConfigureAwait(false)).ToHashSet();
        var validGraphIds = (await TeamAgentDependencyGuard.EnsureGraphIdsAsync(databaseContext, teamId, allGraphIds, cancellationToken).ConfigureAwait(false)).ToHashSet();

        var normalizedSubs = subs.Select(sub => new TeamAgentSubItem
        {
            Key = sub.Key,
            Name = sub.Name,
            Description = sub.Description,
            Prompt = sub.Prompt,
            PluginIds = sub.PluginIds.Where(validPluginIds.Contains).Distinct().ToList(),
            WikiIds = sub.WikiIds.Where(validWikiIds.Contains).Distinct().ToList(),
            GraphIds = sub.GraphIds.Where(validGraphIds.Contains).Distinct().ToList(),
        }).ToList();

        var normalizedMainPluginIds = mainPluginIds.Where(validPluginIds.Contains).Distinct().ToList();
        var normalizedMainWikiIds = mainWikiIds.Where(validWikiIds.Contains).Distinct().ToList();
        var normalizedMainGraphIds = mainGraphIds.Where(validGraphIds.Contains).Distinct().ToList();

        return (normalizedMainPluginIds, normalizedMainWikiIds, normalizedMainGraphIds, normalizedSubs);
    }

    /// <summary>
    /// 校验头像 objectKey 必须是已完成上传并登记的文件，防止伪造（与技能头像同规则）.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    /// <param name="avatar">头像 objectKey，空为跳过.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    public static async Task EnsureAvatarValidAsync(DatabaseContext databaseContext, string? avatar, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(avatar))
        {
            return;
        }

        var avatarFileExists = await databaseContext.Files
            .AnyAsync(f => f.ObjectKey == avatar && f.IsUploaded, cancellationToken);

        if (!avatarFileExists)
        {
            throw new BusinessException("头像文件不存在或未完成上传.") { StatusCode = 404 };
        }
    }
}
