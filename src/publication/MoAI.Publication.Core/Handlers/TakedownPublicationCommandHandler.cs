using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.Database;
using MoAI.Database.Entities;
using MoAI.Database.Enums;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Models;
using MoAI.Publication.Commands;
using MoAI.Team.Services;

namespace MoAI.Publication.Handlers;

/// <summary>
/// <inheritdoc cref="TakedownPublicationCommand"/>
/// </summary>
public class TakedownPublicationCommandHandler : IRequestHandler<TakedownPublicationCommand, EmptyCommandResponse>
{
    private readonly DatabaseContext _databaseContext;
    private readonly ITeamService _teamService;

    /// <summary>
    /// Initializes a new instance of the <see cref="TakedownPublicationCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    /// <param name="teamService">团队领域服务.</param>
    public TakedownPublicationCommandHandler(DatabaseContext databaseContext, ITeamService teamService)
    {
        _databaseContext = databaseContext;
        _teamService = teamService;
    }

    /// <inheritdoc/>
    public async Task<EmptyCommandResponse> Handle(TakedownPublicationCommand request, CancellationToken cancellationToken)
    {
        int teamId;
        long resourceOwnerId = 0;

        if (request.ResourceType == PublicationResourceType.App)
        {
            if (!Guid.TryParse(request.ResourceId, out var appId))
            {
                throw new BusinessException("应用 id 不正确.") { StatusCode = 400 };
            }

            var app = await _databaseContext.Apps
                .FirstOrDefaultAsync(x => x.Id == appId, cancellationToken);

            if (app == null)
            {
                throw new BusinessException("应用不存在或已删除.") { StatusCode = 404 };
            }

            if (!app.IsPublic)
            {
                throw new BusinessException("该应用未公开，无需下架.") { StatusCode = 409 };
            }

            teamId = app.TeamId;
            app.IsPublic = false;
        }
        else if (request.ResourceType == PublicationResourceType.Skill)
        {
            if (!Guid.TryParse(request.ResourceId, out var skillId))
            {
                throw new BusinessException("技能 id 不正确.") { StatusCode = 400 };
            }

            var skill = await _databaseContext.Skills
                .FirstOrDefaultAsync(x => x.Id == skillId, cancellationToken);

            if (skill == null)
            {
                throw new BusinessException("技能不存在或已删除.") { StatusCode = 404 };
            }

            if (skill.IsSystem)
            {
                throw new BusinessException("系统内置技能无需下架.") { StatusCode = 400 };
            }

            if (!skill.IsPublic)
            {
                throw new BusinessException("该技能未公开，无需下架.") { StatusCode = 409 };
            }

            teamId = skill.TeamId;
            resourceOwnerId = skill.CreateUserId;
            skill.IsPublic = false;
        }
        else
        {
            if (!int.TryParse(request.ResourceId, out var promptId))
            {
                throw new BusinessException("提示词 id 不正确.") { StatusCode = 400 };
            }

            var prompt = await _databaseContext.Prompts
                .FirstOrDefaultAsync(x => x.Id == promptId, cancellationToken);

            if (prompt == null)
            {
                throw new BusinessException("提示词不存在或已删除.") { StatusCode = 404 };
            }

            if (!prompt.IsPublic)
            {
                throw new BusinessException("该提示词未公开，无需下架.") { StatusCode = 409 };
            }

            teamId = prompt.TeamId;
            resourceOwnerId = prompt.CreateUserId;
            prompt.IsPublic = false;
        }

        if (teamId == 0)
        {
            // 个人资源（个人提示词/个人技能）仅创建人可下架
            if (resourceOwnerId != request.ContextUserId)
            {
                throw new BusinessException("只有创建人可以下架个人资源.") { StatusCode = 403 };
            }
        }
        else
        {
            var myRole = await _teamService.GetMyRoleAsync(teamId, request.ContextUserId, cancellationToken);

            if (myRole == null)
            {
                throw new BusinessException("团队不存在或你不是团队成员.") { StatusCode = 404 };
            }

            if (myRole == TeamRole.Member)
            {
                throw new BusinessException("只有团队管理员可以下架.") { StatusCode = 403 };
            }
        }

        await _databaseContext.SaveChangesAsync(cancellationToken);

        return EmptyCommandResponse.Default;
    }
}
