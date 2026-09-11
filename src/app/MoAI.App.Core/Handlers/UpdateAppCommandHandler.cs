using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.App.Commands;
using MoAI.Database;
using MoAI.Database.Enums;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Models;
using MoAI.Team.Services;

namespace MoAI.App.Handlers;

/// <summary>
/// <inheritdoc cref="UpdateAppCommand"/>
/// </summary>
public class UpdateAppCommandHandler : IRequestHandler<UpdateAppCommand, EmptyCommandResponse>
{
    private readonly DatabaseContext _databaseContext;
    private readonly ITeamService _teamService;

    /// <summary>
    /// Initializes a new instance of the <see cref="UpdateAppCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    /// <param name="teamService">团队领域服务.</param>
    public UpdateAppCommandHandler(DatabaseContext databaseContext, ITeamService teamService)
    {
        _databaseContext = databaseContext;
        _teamService = teamService;
    }

    /// <inheritdoc/>
    public async Task<EmptyCommandResponse> Handle(UpdateAppCommand request, CancellationToken cancellationToken)
    {
        var app = await _databaseContext.Apps
            .FirstOrDefaultAsync(x => x.Id == request.AppId, cancellationToken);

        if (app == null)
        {
            throw new BusinessException("应用不存在.") { StatusCode = 404 };
        }

        var myRole = await _teamService.GetMyRoleAsync(app.TeamId, request.ContextUserId, cancellationToken);

        if (myRole == null)
        {
            throw new BusinessException("团队不存在或你不是团队成员.") { StatusCode = 404 };
        }

        if (myRole == TeamRole.Member)
        {
            throw new BusinessException("只有团队管理员可以管理应用.") { StatusCode = 403 };
        }

        var nameExist = await _databaseContext.Apps
            .AnyAsync(x => x.TeamId == app.TeamId && x.Name == request.Name && x.Id != app.Id, cancellationToken);

        if (nameExist)
        {
            throw new BusinessException("应用名称已存在，请更换后重试.") { StatusCode = 409 };
        }

        app.Name = request.Name;
        app.Description = request.Description ?? string.Empty;
        app.EnableForeign = request.EnableForeign;
        await _databaseContext.SaveChangesAsync(cancellationToken);

        return EmptyCommandResponse.Default;
    }
}
