using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.App.Commands;
using MoAI.Database;
using MoAI.Database.Entities;
using MoAI.Database.Enums;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Models;
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

        if (app == null)
        {
            throw new BusinessException("应用不存在.") { StatusCode = 404 };
        }

        var myRole = await _teamService.GetMyRoleAsync(app.TeamId, request.ContextUserId, cancellationToken);

        if (myRole == null)
        {
            throw new BusinessException("团队不存在或你不是团队成员.") { StatusCode = 404 };
        }

        if (app.AppType != (int)AppType.Agent)
        {
            throw new BusinessException("只有 Agent 应用支持对话.") { StatusCode = 400 };
        }

        // Member 只能对已发布应用发起会话；管理员可直接测试未发布应用
        if (myRole == TeamRole.Member && app.PublishStatus != 1)
        {
            throw new BusinessException("应用尚未发布，无法对话.") { StatusCode = 403 };
        }

        var session = new AppAgentSessionEntity
        {
            Id = Guid.CreateVersion7(),
            TeamId = app.TeamId,
            AppId = app.Id,
            Title = string.IsNullOrWhiteSpace(request.Title) ? DefaultTitle : request.Title!,
            UserType = (int)request.ContextUserType,
            LastMessageTime = DateTimeOffset.Now,
            CreateUserId = request.ContextUserId,
        };

        _databaseContext.AppAgentSessions.Add(session);
        await _databaseContext.SaveChangesAsync(cancellationToken);

        return new SimpleGuid { Value = session.Id };
    }
}
