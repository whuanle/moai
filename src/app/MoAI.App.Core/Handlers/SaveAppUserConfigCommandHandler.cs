using System.Text.Json;
using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.App.Commands;
using MoAI.App.Services;
using MoAI.Database;
using MoAI.Database.Entities;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Models;
using MoAI.Team.Services;

namespace MoAI.App.Handlers;

/// <summary>
/// <inheritdoc cref="SaveAppUserConfigCommand"/>
/// </summary>
public class SaveAppUserConfigCommandHandler : IRequestHandler<SaveAppUserConfigCommand, EmptyCommandResponse>
{
    private readonly DatabaseContext _databaseContext;
    private readonly ITeamService _teamService;

    /// <summary>
    /// Initializes a new instance of the <see cref="SaveAppUserConfigCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    /// <param name="teamService">团队领域服务.</param>
    public SaveAppUserConfigCommandHandler(DatabaseContext databaseContext, ITeamService teamService)
    {
        _databaseContext = databaseContext;
        _teamService = teamService;
    }

    /// <inheritdoc/>
    public async Task<EmptyCommandResponse> Handle(SaveAppUserConfigCommand request, CancellationToken cancellationToken)
    {
        var app = await _databaseContext.Apps.FirstOrDefaultAsync(x => x.Id == request.AppId, cancellationToken)
            ?? throw new BusinessException("应用不存在.") { StatusCode = 404 };

        if (app.IsDisable)
        {
            throw new BusinessException("应用已被禁用.") { StatusCode = 403 };
        }

        var myRole = await _teamService.GetMyRoleAsync(app.TeamId, request.ContextUserId, cancellationToken);
        var canUseAsPublic = app.IsPublic && app.PublishStatus == 1;
        if (myRole == null && !canUseAsPublic)
        {
            throw new BusinessException("团队不存在或你不是团队成员.") { StatusCode = 404 };
        }

        if (request.PromptId != 0)
        {
            await SessionPromptHelper.EnsureUsableAsync(_databaseContext, _teamService, request.PromptId, app.TeamId, request.ContextUserId, cancellationToken);
        }

        // 应用默认技能集合：用户勾选只能落在该集合内
        var skillsJson = await _databaseContext.AppAgentConfigs.AsNoTracking()
            .Where(x => x.AppId == request.AppId)
            .Select(x => x.Skills)
            .FirstOrDefaultAsync(cancellationToken);
        var defaultSkillIds = AppAgentConfigJson.ParseGuidList(skillsJson).ToHashSet();

        var skills = request.Skills?.Distinct().ToList() ?? new List<Guid>();
        if (skills.Count > 0 && skills.Any(x => !defaultSkillIds.Contains(x)))
        {
            throw new BusinessException("存在应用未开放的技能，请在应用设置中重新选择.") { StatusCode = 400 };
        }

        var userConfig = await _databaseContext.AppUserConfigs
            .FirstOrDefaultAsync(x => x.AppId == request.AppId && x.UserId == request.ContextUserId, cancellationToken);

        if (userConfig == null)
        {
            userConfig = new AppUserConfigEntity
            {
                Id = Guid.CreateVersion7(),
                AppId = request.AppId,
                UserId = request.ContextUserId,
                TeamId = app.TeamId,
                PromptId = request.PromptId,
                Skills = JsonSerializer.Serialize(skills.Select(x => x.ToString()).ToList()),
                ToolApprovalMode = MoAI.AI.AppToolApprovalContract.IsValidMode(request.ToolApprovalMode)
                    ? request.ToolApprovalMode!
                    : MoAI.AI.AppToolApprovalContract.ModeAuto,
            };
            _databaseContext.AppUserConfigs.Add(userConfig);
        }
        else
        {
            userConfig.PromptId = request.PromptId;
            userConfig.Skills = JsonSerializer.Serialize(skills.Select(x => x.ToString()).ToList());
            if (MoAI.AI.AppToolApprovalContract.IsValidMode(request.ToolApprovalMode))
            {
                userConfig.ToolApprovalMode = request.ToolApprovalMode!;
            }
        }

        await _databaseContext.SaveChangesAsync(cancellationToken);
        return EmptyCommandResponse.Default;
    }
}
