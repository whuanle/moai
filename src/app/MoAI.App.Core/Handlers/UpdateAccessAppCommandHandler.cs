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
/// <inheritdoc cref="UpdateAccessAppCommand"/>
/// </summary>
public class UpdateAccessAppCommandHandler : IRequestHandler<UpdateAccessAppCommand, EmptyCommandResponse>
{
    private readonly DatabaseContext _databaseContext;
    private readonly ITeamService _teamService;

    /// <summary>
    /// Initializes a new instance of the <see cref="UpdateAccessAppCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    /// <param name="teamService">团队领域服务.</param>
    public UpdateAccessAppCommandHandler(DatabaseContext databaseContext, ITeamService teamService)
    {
        _databaseContext = databaseContext;
        _teamService = teamService;
    }

    /// <inheritdoc/>
    public async Task<EmptyCommandResponse> Handle(UpdateAccessAppCommand request, CancellationToken cancellationToken)
    {
        var entity = await _databaseContext.AccessApps.FirstOrDefaultAsync(x => x.Id == request.AccessAppId, cancellationToken);

        if (entity == null)
        {
            throw new BusinessException("应用接入不存在.") { StatusCode = 404 };
        }

        var myRole = await _teamService.GetMyRoleAsync(entity.TeamId, request.ContextUserId, cancellationToken);

        if (myRole == null)
        {
            throw new BusinessException("团队不存在或你不是团队成员.") { StatusCode = 404 };
        }

        if (myRole == TeamRole.Member)
        {
            throw new BusinessException("只有团队管理员可以管理应用接入.") { StatusCode = 403 };
        }

        entity.Name = request.Name;
        entity.Description = request.Description ?? string.Empty;
        await _databaseContext.SaveChangesAsync(cancellationToken);

        return EmptyCommandResponse.Default;
    }
}
