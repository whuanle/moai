using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.App.Commands;
using MoAI.Database;
using MoAI.Database.Entities;
using MoAI.Database.Enums;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Models;
using MoAI.App.Services;
using MoAI.Team.Services;

namespace MoAI.App.Handlers;

/// <summary>
/// <inheritdoc cref="CreateAppSessionCommand"/>
/// </summary>
public class CreateAppSessionCommandHandler : IRequestHandler<CreateAppSessionCommand, SimpleGuid>
{
    private const string DefaultTitle = "未命名标题";

    private readonly DatabaseContext _databaseContext;
    private readonly ITeamService _teamService;

    /// <summary>
    /// Initializes a new instance of the <see cref="CreateAppSessionCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    /// <param name="teamService">团队领域服务.</param>
    public CreateAppSessionCommandHandler(DatabaseContext databaseContext, ITeamService teamService)
    {
        _databaseContext = databaseContext;
        _teamService = teamService;
    }

    /// <inheritdoc/>
    public async Task<SimpleGuid> Handle(CreateAppSessionCommand request, CancellationToken cancellationToken)
    {
        var app = await _databaseContext.Apps.FirstOrDefaultAsync(x => x.Id == request.AppId, cancellationToken);

        // 外部应用不通过内部会话使用；内部用户也看不到外部应用
        if (app == null || app.IsExternal)
        {
            throw new BusinessException("应用不存在.") { StatusCode = 404 };
        }

        if (app.IsDisable)
        {
            throw new BusinessException("应用已被禁用.") { StatusCode = 403 };
        }

        // Agent 应用对话模型，Workflow 应用对话执行已发布流程；其他类型不支持会话
        if (app.AppType != (int)AppType.Agent && app.AppType != (int)AppType.Workflow)
        {
            throw new BusinessException("该应用类型不支持对话.") { StatusCode = 400 };
        }

        var myRole = await _teamService.GetMyRoleAsync(app.TeamId, request.ContextUserId, cancellationToken);

        // 非团队成员仅在应用「已发布且公开到平台」时可使用
        var canUseAsPublic = app.IsPublic && app.PublishStatus == 1;
        if (myRole == null && !canUseAsPublic)
        {
            throw new BusinessException("团队不存在或你不是团队成员.") { StatusCode = 404 };
        }

        // Member 只能对已发布应用发起会话；管理员可直接测试未发布应用
        if (myRole == TeamRole.Member && app.PublishStatus != 1)
        {
            throw new BusinessException("应用尚未发布，无法对话.") { StatusCode = 403 };
        }

        if (request.PromptId != 0)
        {
            await SessionPromptHelper.EnsureUsableAsync(_databaseContext, _teamService, request.PromptId, app.TeamId, request.ContextUserId, cancellationToken);
        }

        var session = new AppAgentSessionEntity
        {
            Id = Guid.CreateVersion7(),
            TeamId = app.TeamId,
            AppId = app.Id,
            Title = string.IsNullOrWhiteSpace(request.Title) ? DefaultTitle : request.Title!,
            PromptId = request.PromptId,
            UserType = (int)request.ContextUserType,
            LastMessageTime = DateTimeOffset.Now,
            CreateUserId = request.ContextUserId,
        };

        _databaseContext.AppAgentSessions.Add(session);
        await _databaseContext.SaveChangesAsync(cancellationToken);

        return new SimpleGuid { Value = session.Id };
    }
}
