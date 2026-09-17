using Microsoft.EntityFrameworkCore;
using MoAI.Account.Services;
using MoAI.Database;
using MoAI.Database.Entities;
using MoAI.Database.Enums;
using MoAI.Skill.Queries.Responses;
using MoAI.Skill.Services;

namespace MoAI.Skill.Handlers;

/// <summary>
/// 技能列表映射辅助：实体转列表项并填充创建人/更新人姓名与待审核上架申请.
/// </summary>
internal static class QuerySkillListHelper
{
    /// <summary>
    /// 将技能实体映射为列表项并填充展示信息.
    /// </summary>
    /// <param name="entities">技能实体集合.</param>
    /// <param name="databaseContext">数据库上下文.</param>
    /// <param name="userInfoFillService">用户信息填充领域服务.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>列表项集合.</returns>
    public static async Task<List<SkillListItem>> MapAsync(
        List<SkillEntity> entities,
        DatabaseContext databaseContext,
        IUserInfoFillService userInfoFillService,
        CancellationToken cancellationToken)
    {
        var skillIds = entities.Select(x => x.Id).ToList();
        var skillIdStrings = skillIds.ConvertAll(x => x.ToString());
        var pendingPublicationIds = await databaseContext.PublicationReviews
            .AsNoTracking()
            .Where(x => x.ResourceType == (int)PublicationResourceType.Skill
                && skillIdStrings.Contains(x.ResourceId)
                && x.State == (int)PublicationState.Pending)
            .ToDictionaryAsync(x => x.ResourceId, x => x.Id, cancellationToken);

        var items = entities.Select(x => new SkillListItem
        {
            Id = x.Id,
            Key = x.Key,
            Name = x.Name,
            Description = x.Description,
            IsSystem = x.IsSystem,
            IsDisable = x.IsDisable,
            TeamId = x.TeamId,
            IsPublic = x.IsPublic,
            PendingPublicationId = pendingPublicationIds.TryGetValue(x.Id.ToString(), out var publicationId) ? publicationId : null,
            FileCount = x.IsSystem ? BuiltinSkills.GetFiles(x.Key).Count : SkillService.ParseFiles(x.Files).Count,
            CreateUserId = (int)x.CreateUserId,
            CreateTime = x.CreateTime,
            UpdateUserId = (int)x.UpdateUserId,
            UpdateTime = x.UpdateTime,
        }).ToList();

        await userInfoFillService.FillAsync(items, cancellationToken);
        return items;
    }

    /// <summary>
    /// 按标识/名称/描述关键字过滤.
    /// </summary>
    /// <param name="query">查询源.</param>
    /// <param name="keywords">关键字，空则不过滤.</param>
    /// <returns>过滤后的查询源.</returns>
    public static IQueryable<SkillEntity> WhereKeywords(IQueryable<SkillEntity> query, string? keywords)
    {
        if (!string.IsNullOrWhiteSpace(keywords))
        {
            var pattern = keywords.Trim();
            query = query.Where(x => x.Key.Contains(pattern) || x.Name.Contains(pattern) || x.Description.Contains(pattern));
        }

        return query;
    }
}
