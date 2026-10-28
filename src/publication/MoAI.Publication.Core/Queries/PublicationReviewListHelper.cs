using Microsoft.EntityFrameworkCore;
using MoAI.Account.Services;
using MoAI.Database;
using MoAI.Database.Entities;
using MoAI.Database.Enums;
using MoAI.Publication.Queries.Responses;

namespace MoAI.Publication.Queries;

/// <summary>
/// 上架审核列表映射辅助：批量补齐团队名称并填充申请人/审批人姓名.
/// </summary>
internal static class PublicationReviewListHelper
{
    /// <summary>
    /// 将上架审核实体映射为列表项并填充展示信息.
    /// </summary>
    /// <param name="entities">上架审核实体集合.</param>
    /// <param name="databaseContext">数据库上下文.</param>
    /// <param name="userInfoFillService">用户信息填充领域服务.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>列表项集合.</returns>
    public static async Task<List<PublicationReviewItem>> MapAsync(
        List<PublicationReviewEntity> entities,
        DatabaseContext databaseContext,
        IUserInfoFillService userInfoFillService,
        CancellationToken cancellationToken)
    {
        var teamIds = entities.Select(x => x.TeamId).Distinct().ToList();
        var teamNames = await databaseContext.Teams
            .AsNoTracking()
            .Where(t => teamIds.Contains(t.Id))
            .ToDictionaryAsync(t => t.Id, t => t.Name, cancellationToken);

        var avatars = await BuildResourceAvatarMapAsync(entities, databaseContext, cancellationToken);

        var items = entities.Select(x => new PublicationReviewItem
        {
            PublicationId = x.Id,
            ResourceType = (PublicationResourceType)x.ResourceType,
            ResourceId = x.ResourceId,
            ResourceName = x.ResourceName,
            TeamId = x.TeamId,
            TeamName = teamNames.TryGetValue(x.TeamId, out var teamName) ? teamName : string.Empty,
            ResourceAvatar = avatars.TryGetValue(Key((PublicationResourceType)x.ResourceType, x.ResourceId), out var avatar) ? avatar : string.Empty,
            ApplyReason = x.ApplyReason,
            PublishVersion = x.PublishVersion,
            State = (PublicationState)x.State,
            ReviewComment = x.ReviewComment,
            ReviewTime = x.ReviewTime,
            CreateUserId = (int)x.CreateUserId,
            CreateTime = x.CreateTime,
            UpdateUserId = (int)x.UpdateUserId,
            UpdateTime = x.UpdateTime,
        }).ToList();

        await userInfoFillService.FillAsync(items, cancellationToken);
        return items;
    }

    /// <summary>
    /// 按资源类型批量查询头像（应用=Apps.Avatar，提示词/技能=AvatarPath），key 为 "type:resourceId".
    /// </summary>
    private static async Task<Dictionary<string, string>> BuildResourceAvatarMapAsync(
        List<PublicationReviewEntity> entities,
        DatabaseContext databaseContext,
        CancellationToken cancellationToken)
    {
        var avatars = new Dictionary<string, string>();

        var appIds = entities
            .Where(x => x.ResourceType == (int)PublicationResourceType.App && Guid.TryParse(x.ResourceId, out _))
            .Select(x => Guid.Parse(x.ResourceId))
            .Distinct().ToList();
        if (appIds.Count > 0)
        {
            var appAvatars = await databaseContext.Apps
                .AsNoTracking()
                .Where(a => appIds.Contains(a.Id))
                .Select(a => new { a.Id, a.Avatar })
                .ToListAsync(cancellationToken);
            foreach (var row in appAvatars)
            {
                avatars[Key(PublicationResourceType.App, row.Id.ToString())] = row.Avatar ?? string.Empty;
            }
        }

        var promptIds = entities
            .Where(x => x.ResourceType == (int)PublicationResourceType.Prompt && int.TryParse(x.ResourceId, out _))
            .Select(x => int.Parse(x.ResourceId))
            .Distinct().ToList();
        if (promptIds.Count > 0)
        {
            var promptAvatars = await databaseContext.Prompts
                .AsNoTracking()
                .Where(x => promptIds.Contains(x.Id))
                .Select(x => new { x.Id, x.AvatarPath })
                .ToListAsync(cancellationToken);
            foreach (var row in promptAvatars)
            {
                avatars[Key(PublicationResourceType.Prompt, row.Id.ToString())] = row.AvatarPath ?? string.Empty;
            }
        }

        var skillIds = entities
            .Where(x => x.ResourceType == (int)PublicationResourceType.Skill && Guid.TryParse(x.ResourceId, out _))
            .Select(x => Guid.Parse(x.ResourceId))
            .Distinct().ToList();
        if (skillIds.Count > 0)
        {
            var skillAvatars = await databaseContext.Skills
                .AsNoTracking()
                .Where(x => skillIds.Contains(x.Id))
                .Select(x => new { x.Id, x.AvatarPath })
                .ToListAsync(cancellationToken);
            foreach (var row in skillAvatars)
            {
                avatars[Key(PublicationResourceType.Skill, row.Id.ToString())] = row.AvatarPath ?? string.Empty;
            }
        }

        return avatars;
    }

    private static string Key(PublicationResourceType type, string resourceId) => $"{(int)type}:{resourceId}";
}
