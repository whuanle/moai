using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.AI.Services;
using MoAI.App.Commands;
using MoAI.Database;
using MoAI.Database.Enums;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Models;
using MoAI.Team.Services;

namespace MoAI.App.Handlers;

/// <summary>
/// <inheritdoc cref="CreateDebugSessionCommand"/>
/// </summary>
public class CreateDebugSessionCommandHandler : IRequestHandler<CreateDebugSessionCommand, SimpleGuid>
{
    private readonly DatabaseContext _databaseContext;
    private readonly ITeamService _teamService;
    private readonly IDebugSessionRegistry _debugSessionRegistry;

    /// <summary>
    /// Initializes a new instance of the <see cref="CreateDebugSessionCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    /// <param name="teamService">团队领域服务.</param>
    /// <param name="debugSessionRegistry">调试会话注册表.</param>
    public CreateDebugSessionCommandHandler(
        DatabaseContext databaseContext,
        ITeamService teamService,
        IDebugSessionRegistry debugSessionRegistry)
    {
        _databaseContext = databaseContext;
        _teamService = teamService;
        _debugSessionRegistry = debugSessionRegistry;
    }

    /// <inheritdoc/>
    public async Task<SimpleGuid> Handle(CreateDebugSessionCommand request, CancellationToken cancellationToken)
    {
        var app = await _databaseContext.Apps.FirstOrDefaultAsync(x => x.Id == request.AppId, cancellationToken);

        if (app == null)
        {
            throw new BusinessException("应用不存在.") { StatusCode = 404 };
        }

        if (app.IsDisable)
        {
            throw new BusinessException("应用已被禁用.") { StatusCode = 403 };
        }

        if (app.AppType != (int)AppType.Agent)
        {
            throw new BusinessException("只有 Agent 应用支持调试.") { StatusCode = 400 };
        }

        var myRole = await _teamService.GetMyRoleAsync(app.TeamId, request.ContextUserId, cancellationToken);
        if (myRole == null)
        {
            throw new BusinessException("团队不存在或你不是团队成员.") { StatusCode = 404 };
        }

        if (myRole == TeamRole.Member)
        {
            throw new BusinessException("需要团队管理员才能调试应用.") { StatusCode = 403 };
        }

        var sessionId = Guid.CreateVersion7();
        await _debugSessionRegistry.CreateAsync(sessionId, app.Id, app.TeamId, request.ContextUserId, cancellationToken);

        return new SimpleGuid { Value = sessionId };
    }
}
