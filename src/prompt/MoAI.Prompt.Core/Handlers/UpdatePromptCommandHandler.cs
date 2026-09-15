using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.Classify;
using MoAI.Database;
using MoAI.Database.Enums;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Models;
using MoAI.Prompt.Commands;
using MoAI.Team.Services;

namespace MoAI.Prompt.Handlers;

/// <summary>
/// <inheritdoc cref="UpdatePromptCommand"/>
/// </summary>
public class UpdatePromptCommandHandler : IRequestHandler<UpdatePromptCommand, EmptyCommandResponse>
{
    private readonly DatabaseContext _databaseContext;
    private readonly ITeamService _teamService;

    /// <summary>
    /// Initializes a new instance of the <see cref="UpdatePromptCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    /// <param name="teamService">团队领域服务.</param>
    public UpdatePromptCommandHandler(DatabaseContext databaseContext, ITeamService teamService)
    {
        _databaseContext = databaseContext;
        _teamService = teamService;
    }

    /// <inheritdoc/>
    public async Task<EmptyCommandResponse> Handle(UpdatePromptCommand request, CancellationToken cancellationToken)
    {
        var prompt = await _databaseContext.Prompts
            .FirstOrDefaultAsync(x => x.Id == request.PromptId, cancellationToken);

        if (prompt == null)
        {
            throw new BusinessException("提示词不存在.") { StatusCode = 404 };
        }

        if (request.PromptClassId > 0)
        {
            var classifyExist = await _databaseContext.Classifies
                .AnyAsync(x => x.Id == request.PromptClassId && x.Type == ClassifyTypes.Prompt, cancellationToken);

            if (!classifyExist)
            {
                throw new BusinessException("提示词分类不存在.") { StatusCode = 404 };
            }
        }

        if (prompt.TeamId == 0)
        {
            // 个人提示词仅创建人可修改
            if (prompt.CreateUserId != request.ContextUserId)
            {
                throw new BusinessException("只有创建人可以修改个人提示词.") { StatusCode = 403 };
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
                throw new BusinessException("只有团队管理员可以修改团队提示词.") { StatusCode = 403 };
            }
        }

        prompt.Name = request.Name;
        prompt.Description = request.Description ?? string.Empty;
        prompt.Content = request.Content;
        prompt.PromptClassId = request.PromptClassId;
        prompt.AvatarPath = request.AvatarPath ?? string.Empty;

        await _databaseContext.SaveChangesAsync(cancellationToken);

        return EmptyCommandResponse.Default;
    }
}
