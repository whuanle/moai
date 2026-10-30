using Microsoft.EntityFrameworkCore;
using MoAI.Database;

namespace MoAI.AIChannel.Services;

/// <summary>
/// 团队可用模型查询扩展：统一"团队能够使用哪些模型"的聚合口径，
/// = 公开模型 ∪ 已授权给该团队的模型 ∪ 团队自有渠道（ai_channel.team_id=团队id）下的模型，且模型与渠道均启用.
/// </summary>
public static class TeamModelQueryExtensions
{
    /// <summary>
    /// 查询团队可用模型 id 集合，各业务（网关/应用/知识库/知识图谱/记忆等）按同一口径过滤模型.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    /// <param name="teamId">团队 id.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>返回可用模型 id 集合.</returns>
    public static async Task<List<Guid>> GetTeamUsableModelIdsAsync(this DatabaseContext databaseContext, int teamId, CancellationToken cancellationToken = default)
    {
        var authorizedModelIds = await databaseContext.AiModelAuthorizations
            .Where(x => x.TeamId == teamId)
            .Select(x => x.AiModelId)
            .ToListAsync(cancellationToken);

        var query = from m in databaseContext.AiModels
                    join c in databaseContext.AiChannels on m.ChannelId equals c.Id
                    where m.Enabled && c.Enabled && (c.TeamId == teamId || m.IsPublic || authorizedModelIds.Contains(m.Id))
                    select m.Id;

        return await query.ToListAsync(cancellationToken);
    }

    /// <summary>
    /// 判断单个模型是否在团队可用口径内（团队自有渠道 / 公开 / 已授权，模型与渠道均启用）.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    /// <param name="teamId">团队 id.</param>
    /// <param name="modelId">模型 id.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>可用返回 true.</returns>
    public static async Task<bool> IsTeamUsableModelAsync(this DatabaseContext databaseContext, int teamId, Guid modelId, CancellationToken cancellationToken = default)
    {
        var candidate = await (from m in databaseContext.AiModels
                               join c in databaseContext.AiChannels on m.ChannelId equals c.Id
                               where m.Id == modelId && m.Enabled && c.Enabled
                               select new { m.IsPublic, ChannelTeamId = c.TeamId })
            .FirstOrDefaultAsync(cancellationToken);
        if (candidate == null)
        {
            return false;
        }

        if (candidate.IsPublic || candidate.ChannelTeamId == teamId)
        {
            return true;
        }

        return await databaseContext.AiModelAuthorizations
            .AnyAsync(x => x.AiModelId == modelId && x.TeamId == teamId, cancellationToken);
    }
}
