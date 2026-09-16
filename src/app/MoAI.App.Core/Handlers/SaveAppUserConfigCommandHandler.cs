using System.Text.Json;
using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.App.Commands;
using MoAI.App.Services;
using MoAI.Database;
using MoAI.Database.Entities;
using MoAI.Database.Enums;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Models;
using MoAI.Skill.Services;
using MoAI.Team.Services;

namespace MoAI.App.Handlers;

/// <summary>
/// <inheritdoc cref="SaveAppUserConfigCommand"/>
/// </summary>
public class SaveAppUserConfigCommandHandler : IRequestHandler<SaveAppUserConfigCommand, EmptyCommandResponse>
{
    private readonly DatabaseContext _databaseContext;
    private readonly ITeamService _teamService;
    private readonly ISkillService _skillService;

    /// <summary>
    /// Initializes a new instance of the <see cref="SaveAppUserConfigCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    /// <param name="teamService">团队领域服务.</param>
    /// <param name="skillService">技能领域服务.</param>
    public SaveAppUserConfigCommandHandler(DatabaseContext databaseContext, ITeamService teamService, ISkillService skillService)
    {
        _databaseContext = databaseContext;
        _teamService = teamService;
        _skillService = skillService;
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

        var skills = request.Skills?.Distinct().ToList() ?? new List<Guid>();
        if (skills.Count > 0)
        {
            var visible = await _skillService.FilterVisibleSkillIdsAsync(skills, request.ContextUserId, app.TeamId, cancellationToken);
            if (visible.Count != skills.Count)
            {
                throw new BusinessException("存在不可用或无权使用的技能.") { StatusCode = 400 };
            }
        }

        var config = await _databaseContext.AppUserConfigs
            .FirstOrDefaultAsync(x => x.AppId == request.AppId && x.UserId == request.ContextUserId, cancellationToken);

        if (config == null)
        {
            config = new AppUserConfigEntity
            {
                Id = Guid.CreateVersion7(),
                AppId = request.AppId,
                UserId = request.ContextUserId,
                TeamId = app.TeamId,
                PromptId = request.PromptId,
                Skills = JsonSerializer.Serialize(skills.Select(x => x.ToString()).ToList()),
            };
            _databaseContext.AppUserConfigs.Add(config);
        }
        else
        {
            config.PromptId = request.PromptId;
            config.Skills = JsonSerializer.Serialize(skills.Select(x => x.ToString()).ToList());
        }

        await _databaseContext.SaveChangesAsync(cancellationToken);
        return EmptyCommandResponse.Default;
    }
}
