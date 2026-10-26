using System;
using System.Linq;
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
/// <inheritdoc cref="ApplyPublicationCommand"/>
/// </summary>
public class ApplyPublicationCommandHandler : IRequestHandler<ApplyPublicationCommand, SimpleLong>
{
    private readonly DatabaseContext _databaseContext;
    private readonly ITeamService _teamService;

    /// <summary>
    /// Initializes a new instance of the <see cref="ApplyPublicationCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    /// <param name="teamService">团队领域服务.</param>
    public ApplyPublicationCommandHandler(DatabaseContext databaseContext, ITeamService teamService)
    {
        _databaseContext = databaseContext;
        _teamService = teamService;
    }

    /// <inheritdoc/>
    public async Task<SimpleLong> Handle(ApplyPublicationCommand request, CancellationToken cancellationToken)
    {
        int teamId;
        string resourceName;
        long resourceOwnerId = 0;
        int? publishVersionValue = null;

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
                throw new BusinessException("应用不存在.") { StatusCode = 404 };
            }

            if (app.IsExternal)
            {
                throw new BusinessException("外部应用不支持公开到平台.") { StatusCode = 400 };
            }

            if (app.IsPublic)
            {
                throw new BusinessException("应用已公开，无需重复申请.") { StatusCode = 409 };
            }

            // 应用按版本号上架：aaa.bb.cc 编码为 int 落库（规则与 MoAI.App.Shared.AppVersionHelper 一致），格式已由命令校验、此处防御性再验
            if (!TryParseVersion(request.Version, out var publishVersion))
            {
                throw new BusinessException("应用版本号格式不正确，应为 aaa.bb.cc（如 1.2.0）.") { StatusCode = 400 };
            }

            publishVersionValue = publishVersion;
            teamId = app.TeamId;
            resourceName = app.Name;
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
                throw new BusinessException("技能不存在.") { StatusCode = 404 };
            }

            if (skill.IsSystem)
            {
                throw new BusinessException("系统内置技能无需申请上架.") { StatusCode = 400 };
            }

            if (skill.IsPublic)
            {
                throw new BusinessException("技能已公开，无需重复申请.") { StatusCode = 409 };
            }

            teamId = skill.TeamId;
            resourceName = skill.Name;
            resourceOwnerId = skill.CreateUserId;
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
                throw new BusinessException("提示词不存在.") { StatusCode = 404 };
            }

            if (prompt.IsPublic)
            {
                throw new BusinessException("提示词已公开，无需重复申请.") { StatusCode = 409 };
            }

            teamId = prompt.TeamId;
            resourceName = prompt.Name;
            resourceOwnerId = prompt.CreateUserId;
        }

        if (teamId == 0)
        {
            // 个人技能/个人提示词仅创建人可申请上架
            if (resourceOwnerId != request.ContextUserId)
            {
                throw new BusinessException("只有创建人可以申请上架个人资源.") { StatusCode = 403 };
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
                throw new BusinessException("只有团队管理员可以申请上架.") { StatusCode = 403 };
            }
        }

        var pendingExist = await _databaseContext.PublicationReviews
            .AnyAsync(x => x.ResourceType == (int)request.ResourceType
                && x.ResourceId == request.ResourceId
                && x.State == (int)PublicationState.Pending, cancellationToken);

        if (pendingExist)
        {
            throw new BusinessException("该资源已有待审核的上架申请，请耐心等待审批.") { StatusCode = 409 };
        }

        var publicationReview = new PublicationReviewEntity
        {
            ResourceType = (int)request.ResourceType,
            ResourceId = request.ResourceId,
            ResourceName = resourceName,
            TeamId = teamId,
            ApplyReason = request.ApplyReason ?? string.Empty,
            PublishVersion = publishVersionValue,
            State = (int)PublicationState.Pending
        };

        _databaseContext.PublicationReviews.Add(publicationReview);
        await _databaseContext.SaveChangesAsync(cancellationToken);

        return new SimpleLong { Value = publicationReview.Id };
    }

    /// <summary>
    /// 解析 aaa.bb.cc 文本为 int 编码（aaa*10000 + bb*100 + cc），规则与 MoAI.App.Shared.AppVersionHelper 保持一致.
    /// </summary>
    private static bool TryParseVersion(string? text, out int version)
    {
        version = 0;
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var parts = text.Split('.');
        if (parts.Length != 3
            || !TryParsePart(parts[0], 999, out var aaa)
            || !TryParsePart(parts[1], 99, out var bb)
            || !TryParsePart(parts[2], 99, out var cc))
        {
            return false;
        }

        version = (aaa * 10000) + (bb * 100) + cc;
        return true;
    }

    private static bool TryParsePart(string text, int maxValue, out int value)
    {
        value = 0;
        if (text.Length == 0 || text.Length > 3 || !text.All(char.IsAsciiDigit) || !int.TryParse(text, out value))
        {
            return false;
        }

        return value <= maxValue;
    }
}
