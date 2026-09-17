using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.Database;
using MoAI.Database.Enums;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Models;
using MoAI.Publication.Commands;
using MoAI.Team.Services;

namespace MoAI.Publication.Handlers;

/// <summary>
/// <inheritdoc cref="WithdrawPublicationCommand"/>
/// </summary>
public class WithdrawPublicationCommandHandler : IRequestHandler<WithdrawPublicationCommand, EmptyCommandResponse>
{
    private readonly DatabaseContext _databaseContext;
    private readonly ITeamService _teamService;

    /// <summary>
    /// Initializes a new instance of the <see cref="WithdrawPublicationCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    /// <param name="teamService">团队领域服务.</param>
    public WithdrawPublicationCommandHandler(DatabaseContext databaseContext, ITeamService teamService)
    {
        _databaseContext = databaseContext;
        _teamService = teamService;
    }

    /// <inheritdoc/>
    public async Task<EmptyCommandResponse> Handle(WithdrawPublicationCommand request, CancellationToken cancellationToken)
    {
        var publicationReview = await _databaseContext.PublicationReviews
            .FirstOrDefaultAsync(x => x.Id == request.PublicationId, cancellationToken);

        if (publicationReview == null)
        {
            throw new BusinessException("上架申请不存在.") { StatusCode = 404 };
        }

        if (publicationReview.State != (int)PublicationState.Pending)
        {
            throw new BusinessException("仅待审核的上架申请可以撤回.") { StatusCode = 409 };
        }

        if (publicationReview.TeamId == 0)
        {
            // 个人资源（个人提示词/个人技能）的上架申请仅创建人可撤回
            if ((PublicationResourceType)publicationReview.ResourceType == PublicationResourceType.Skill)
            {
                if (!Guid.TryParse(publicationReview.ResourceId, out var skillId))
                {
                    throw new BusinessException("技能 id 不正确.") { StatusCode = 400 };
                }

                var skill = await _databaseContext.Skills
                    .FirstOrDefaultAsync(x => x.Id == skillId, cancellationToken);

                if (skill == null)
                {
                    throw new BusinessException("技能不存在或已删除.") { StatusCode = 404 };
                }

                if (skill.CreateUserId != request.ContextUserId)
                {
                    throw new BusinessException("只有创建人可以撤回个人技能的上架申请.") { StatusCode = 403 };
                }
            }
            else
            {
                if (!int.TryParse(publicationReview.ResourceId, out var promptId))
                {
                    throw new BusinessException("提示词 id 不正确.") { StatusCode = 400 };
                }

                var prompt = await _databaseContext.Prompts
                    .FirstOrDefaultAsync(x => x.Id == promptId, cancellationToken);

                if (prompt == null)
                {
                    throw new BusinessException("提示词不存在或已删除.") { StatusCode = 404 };
                }

                if (prompt.CreateUserId != request.ContextUserId)
                {
                    throw new BusinessException("只有创建人可以撤回个人提示词的上架申请.") { StatusCode = 403 };
                }
            }
        }
        else
        {
            var myRole = await _teamService.GetMyRoleAsync(publicationReview.TeamId, request.ContextUserId, cancellationToken);

            if (myRole == null)
            {
                throw new BusinessException("团队不存在或你不是团队成员.") { StatusCode = 404 };
            }

            if (myRole == TeamRole.Member)
            {
                throw new BusinessException("只有团队管理员可以撤回上架申请.") { StatusCode = 403 };
            }
        }

        _databaseContext.PublicationReviews.Remove(publicationReview);
        await _databaseContext.SaveChangesAsync(cancellationToken);

        return EmptyCommandResponse.Default;
    }
}
