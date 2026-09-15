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

        var items = entities.Select(x => new PublicationReviewItem
        {
            PublicationId = x.Id,
            ResourceType = (PublicationResourceType)x.ResourceType,
            ResourceId = x.ResourceId,
            ResourceName = x.ResourceName,
            TeamId = x.TeamId,
            TeamName = teamNames.TryGetValue(x.TeamId, out var teamName) ? teamName : string.Empty,
            ApplyReason = x.ApplyReason,
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
}
