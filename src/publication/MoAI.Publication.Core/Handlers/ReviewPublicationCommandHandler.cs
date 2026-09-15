using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.Database;
using MoAI.Database.Enums;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Models;
using MoAI.Publication.Commands;

namespace MoAI.Publication.Handlers;

/// <summary>
/// <inheritdoc cref="ReviewPublicationCommand"/>
/// </summary>
public class ReviewPublicationCommandHandler : IRequestHandler<ReviewPublicationCommand, EmptyCommandResponse>
{
    private readonly DatabaseContext _databaseContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="ReviewPublicationCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    public ReviewPublicationCommandHandler(DatabaseContext databaseContext)
    {
        _databaseContext = databaseContext;
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
        publicationReview.ReviewTime = DateTimeOffset.Now;
        await _databaseContext.SaveChangesAsync(cancellationToken);

        return EmptyCommandResponse.Default;
    }
}
