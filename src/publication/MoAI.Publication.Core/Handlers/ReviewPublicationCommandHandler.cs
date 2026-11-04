using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.Database;
using MoAI.Database.Enums;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Models;
using MoAI.Org.Services;
using MoAI.Publication.Commands;
using MoAI.Publication.Queries;

namespace MoAI.Publication.Handlers;

/// <summary>
/// <inheritdoc cref="ReviewPublicationCommand"/>
/// </summary>
public class ReviewPublicationCommandHandler : IRequestHandler<ReviewPublicationCommand, EmptyCommandResponse>
{
    private readonly DatabaseContext _databaseContext;
    private readonly IOrgGrantService _orgGrantService;

    /// <summary>
    /// Initializes a new instance of the <see cref="ReviewPublicationCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    /// <param name="orgGrantService">统一组织授权领域服务.</param>
    public ReviewPublicationCommandHandler(DatabaseContext databaseContext, IOrgGrantService orgGrantService)
    {
        _databaseContext = databaseContext;
        _orgGrantService = orgGrantService;
    }

    /// <inheritdoc/>
    public async Task<EmptyCommandResponse> Handle(ReviewPublicationCommand request, CancellationToken cancellationToken)
    {
        var publicationReview = await _databaseContext.PublicationReviews
            .FirstOrDefaultAsync(x => x.Id == request.PublicationId, cancellationToken);

        if (publicationReview == null)
        {
            throw new BusinessException("上架申请不存在.") { StatusCode = 404 };
        }

        if (publicationReview.State != (int)PublicationState.Pending)
        {
            throw new BusinessException("该上架申请已审批，请刷新后重试.") { StatusCode = 409 };
        }

        // 应用审批通过后待物化的可视范围（统一在保存后物化，授权服务内部保存，避免中间状态一并持久化）
        Guid? materializeAppId = null;
        if (request.IsApprove)
        {
            // 审批通过：将目标资源 is_public 置为 true；资源已被删除时无法通过，只能驳回
            if ((PublicationResourceType)publicationReview.ResourceType == PublicationResourceType.App)
            {
                var appId = Guid.Parse(publicationReview.ResourceId);
                var app = await _databaseContext.Apps
                    .FirstOrDefaultAsync(x => x.Id == appId, cancellationToken);

                if (app == null)
                {
                    throw new BusinessException("应用不存在或已删除，无法通过上架.") { StatusCode = 404 };
                }

                app.IsPublic = true;
                materializeAppId = appId;
            }
            else if ((PublicationResourceType)publicationReview.ResourceType == PublicationResourceType.Skill)
            {
                var skillId = Guid.Parse(publicationReview.ResourceId);
                var skill = await _databaseContext.Skills
                    .FirstOrDefaultAsync(x => x.Id == skillId, cancellationToken);

                if (skill == null)
                {
                    throw new BusinessException("技能不存在或已删除，无法通过上架.") { StatusCode = 404 };
                }

                skill.IsPublic = true;
            }
            else
            {
                var promptId = int.Parse(publicationReview.ResourceId);
                var prompt = await _databaseContext.Prompts
                    .FirstOrDefaultAsync(x => x.Id == promptId, cancellationToken);

                if (prompt == null)
                {
                    throw new BusinessException("提示词不存在或已删除，无法通过上架.") { StatusCode = 404 };
                }

                prompt.IsPublic = true;
            }

            publicationReview.State = (int)PublicationState.Approved;
        }
        else
        {
            publicationReview.State = (int)PublicationState.Rejected;
        }

        publicationReview.ReviewComment = request.ReviewComment ?? string.Empty;
        publicationReview.ReviewTime = DateTime.UtcNow;
        await _databaseContext.SaveChangesAsync(cancellationToken);

        if (materializeAppId.HasValue)
        {
            // 物化上架可视范围到组织授权（publicApp 资源）：全员=清空陈旧授权（无记录即全员），按组织=整体覆盖
            var orgGrants = VisibleOrgGrantSnapshot.Deserialize(publicationReview.VisibleOrgGrants)
                .Select(x => new OrgGrantItem { OrgRoleId = x.OrgRoleId, Scope = x.Scope })
                .ToList();
            await _orgGrantService.ReplaceGrantsAsync(OrgGrantResourceType.PublicApp, materializeAppId.Value, orgGrants, cancellationToken);
        }

        return EmptyCommandResponse.Default;
    }
}
