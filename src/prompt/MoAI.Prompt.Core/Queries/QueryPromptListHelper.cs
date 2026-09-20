using Microsoft.EntityFrameworkCore;
using MoAI.Account.Services;
using MoAI.Database;
using MoAI.Database.Entities;
using MoAI.Database.Enums;
using MoAI.Prompt.Queries.Responses;

namespace MoAI.Prompt.Queries;

/// <summary>
/// 提示词列表映射辅助：实体转列表项并填充创建人/更新人姓名.
/// </summary>
internal static class QueryPromptListHelper
{
    /// <summary>
    /// 将提示词实体映射为列表项（不含内容）并填充展示信息.
    /// </summary>
    /// <param name="entities">提示词实体集合.</param>
    /// <param name="databaseContext">数据库上下文.</param>
    /// <param name="userInfoFillService">用户信息填充领域服务.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>列表项集合.</returns>
    public static async Task<List<PromptItem>> MapAsync(
        List<PromptEntity> entities,
        DatabaseContext databaseContext,
        IUserInfoFillService userInfoFillService,
        CancellationToken cancellationToken)
    {
        var promptIds = entities.Select(x => x.Id).ToList();
        var promptIdStrings = promptIds.ConvertAll(x => x.ToString());
        var pendingPublicationIds = await databaseContext.PublicationReviews
            .AsNoTracking()
            .Where(x => x.ResourceType == (int)PublicationResourceType.Prompt
                && promptIdStrings.Contains(x.ResourceId)
                && x.State == (int)PublicationState.Pending)
            .ToDictionaryAsync(x => x.ResourceId, x => x.Id, cancellationToken);

        var items = entities.Select(x => new PromptItem
        {
            PromptId = x.Id,
            Name = x.Name,
            Description = x.Description,
            AvatarPath = x.AvatarPath,
            PromptClassId = x.PromptClassId,
            IsPublic = x.IsPublic,
            Counter = x.Counter,
            UseCount = x.UseCount,
            TeamId = x.TeamId,
            PendingPublicationId = pendingPublicationIds.TryGetValue(x.Id.ToString(), out var publicationId) ? publicationId : null,
            CreateUserId = (int)x.CreateUserId,
            CreateTime = x.CreateTime,
            UpdateUserId = (int)x.UpdateUserId,
            UpdateTime = x.UpdateTime,
        }).ToList();

        await userInfoFillService.FillAsync(items, cancellationToken);
        return items;
    }

    /// <summary>
    /// 按名称/描述关键字过滤.
    /// </summary>
    /// <param name="query">查询源.</param>
    /// <param name="keywords">关键字，空则不过滤.</param>
    /// <returns>过滤后的查询源.</returns>
    public static IQueryable<PromptEntity> WhereKeywords(IQueryable<PromptEntity> query, string? keywords)
    {
        if (!string.IsNullOrWhiteSpace(keywords))
        {
            var pattern = keywords.Trim();
            query = query.Where(x => x.Name.Contains(pattern) || x.Description.Contains(pattern));
        }

        return query;
    }
}
