using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.Database;
using MoAI.Database.Enums;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Models;
using MoAI.Prompt.Commands;
using MoAI.Team.Services;

namespace MoAI.Prompt.Handlers;

/// <summary>
/// <inheritdoc cref="DeletePromptCommand"/>
/// </summary>
public class DeletePromptCommandHandler : IRequestHandler<DeletePromptCommand, EmptyCommandResponse>
{
    private readonly DatabaseContext _databaseContext;
    private readonly ITeamService _teamService;

    /// <summary>
    /// Initializes a new instance of the <see cref="DeletePromptCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    /// <param name="teamService">团队领域服务.</param>
    public DeletePromptCommandHandler(DatabaseContext databaseContext, ITeamService teamService)
    {
        _databaseContext = databaseContext;
        _teamService = teamService;
    }

    /// <inheritdoc/>
    public async Task<EmptyCommandResponse> Handle(DeletePromptCommand request, CancellationToken cancellationToken)
    {
        var prompt = await _databaseContext.Prompts
            .FirstOrDefaultAsync(x => x.Id == request.PromptId, cancellationToken);

        if (prompt == null)
        {
            throw new BusinessException("提示词不存在.") { StatusCode = 404 };
        }

        if (prompt.TeamId == 0)
        {
            // 个人提示词仅创建人可删除
            if (prompt.CreateUserId != request.ContextUserId)
            {
                throw new BusinessException("只有创建人可以删除个人提示词.") { StatusCode = 403 };
            }
        }
        else
        {
            // 团队提示词需要团队 Admin 及以上角色
            var myRole = await _teamService.GetMyRoleAsync(prompt.TeamId, request.ContextUserId, cancellationToken);

            if (myRole == null)
            {
                throw new BusinessException("你不是该团队成员.") { StatusCode = 403 };
            }

            if (myRole == TeamRole.Member)
            {
                throw new BusinessException("只有团队管理员可以删除团队提示词.") { StatusCode = 403 };
            }
        }

        _databaseContext.Prompts.Remove(prompt);

        // 同步移除该提示词待审核的上架申请，避免审核列表出现僵尸记录
        var promptIdString = prompt.Id.ToString();
        var pendingReviews = await _databaseContext.PublicationReviews
            .Where(x => x.ResourceType == (int)PublicationResourceType.Prompt
                && x.ResourceId == promptIdString
                && x.State == (int)PublicationState.Pending)
            .ToListAsync(cancellationToken);

        _databaseContext.PublicationReviews.RemoveRange(pendingReviews);

        await _databaseContext.SaveChangesAsync(cancellationToken);

        return EmptyCommandResponse.Default;
    }
}
