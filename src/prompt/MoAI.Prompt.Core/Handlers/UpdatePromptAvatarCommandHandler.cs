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
/// <inheritdoc cref="UpdatePromptAvatarCommand"/>
/// </summary>
public class UpdatePromptAvatarCommandHandler : IRequestHandler<UpdatePromptAvatarCommand, EmptyCommandResponse>
{
    private readonly DatabaseContext _databaseContext;
    private readonly ITeamService _teamService;

    /// <summary>
    /// Initializes a new instance of the <see cref="UpdatePromptAvatarCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    /// <param name="teamService">团队领域服务.</param>
    public UpdatePromptAvatarCommandHandler(DatabaseContext databaseContext, ITeamService teamService)
    {
        _databaseContext = databaseContext;
        _teamService = teamService;
    }

    /// <inheritdoc/>
    public async Task<EmptyCommandResponse> Handle(UpdatePromptAvatarCommand request, CancellationToken cancellationToken)
    {
        // 仅允许引用已完成上传并登记的文件，防止任意伪造 objectKey（与团队/用户头像同规则）
        var fileExists = await _databaseContext.Files
            .AnyAsync(f => f.ObjectKey == request.ObjectKey && f.IsUploaded, cancellationToken);

        if (!fileExists)
        {
            throw new BusinessException("头像文件不存在或未完成上传.") { StatusCode = 404 };
        }

        var prompt = await _databaseContext.Prompts
            .FirstOrDefaultAsync(x => x.Id == request.PromptId, cancellationToken);

        if (prompt == null)
        {
            throw new BusinessException("提示词不存在.") { StatusCode = 404 };
        }

        if (prompt.TeamId == 0)
        {
            // 个人提示词仅创建人可设置头像
            if (prompt.CreateUserId != request.ContextUserId)
            {
                throw new BusinessException("只有创建人可以设置个人提示词头像.") { StatusCode = 403 };
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
                throw new BusinessException("只有团队管理员可以设置团队提示词头像.") { StatusCode = 403 };
            }
        }

        prompt.AvatarPath = request.ObjectKey;
        await _databaseContext.SaveChangesAsync(cancellationToken);

        return EmptyCommandResponse.Default;
    }
}
