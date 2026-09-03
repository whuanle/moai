using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.Database;
using MoAI.Database.Enums;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Models;
using MoAI.Infra.Services;
using MoAI.Team.Commands;
using MoAI.Team.Services;

namespace MoAI.Team.Handlers;

/// <summary>
/// <inheritdoc cref="UpdateTeamOwnerCommand"/>
/// </summary>
public class UpdateTeamOwnerCommandHandler : IRequestHandler<UpdateTeamOwnerCommand, EmptyCommandResponse>
{
    private readonly DatabaseContext _databaseContext;
    private readonly ITeamService _teamService;
    private readonly IUserContextProvider _userContextProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="UpdateTeamOwnerCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    /// <param name="teamService">团队领域服务.</param>
    /// <param name="userContextProvider">用户上下文提供者.</param>
    public UpdateTeamOwnerCommandHandler(DatabaseContext databaseContext, ITeamService teamService, IUserContextProvider userContextProvider)
    {
        _databaseContext = databaseContext;
        _teamService = teamService;
        _userContextProvider = userContextProvider;
    }

    /// <inheritdoc/>
    public async Task<EmptyCommandResponse> Handle(UpdateTeamOwnerCommand request, CancellationToken cancellationToken)
    {
        var userId = _userContextProvider.GetUserContext().UserId;
        var myRole = await _teamService.GetMyRoleAsync(request.TeamId, userId, cancellationToken);

        if (myRole == null)
        {
            throw new BusinessException("团队不存在或你不是团队成员.") { StatusCode = 404 };
        }

        if (myRole != TeamRole.Owner)
        {
            throw new BusinessException("只有团队所有者可以转让所有权.") { StatusCode = 403 };
        }

        if (request.UserId == userId)
        {
            throw new BusinessException("不能转让给自己.") { StatusCode = 400 };
        }

        var target = await _databaseContext.TeamUsers
            .FirstOrDefaultAsync(x => x.TeamId == request.TeamId && x.UserId == request.UserId, cancellationToken);

        if (target == null)
        {
            throw new BusinessException("目标用户不是团队成员.") { StatusCode = 404 };
        }

        // 原所有者降为 Admin，目标成员升为 Owner
        var currentOwner = await _databaseContext.TeamUsers
            .FirstAsync(x => x.TeamId == request.TeamId && x.UserId == userId, cancellationToken);

        currentOwner.Role = TeamRole.Admin;
        target.Role = TeamRole.Owner;

        await _databaseContext.SaveChangesAsync(cancellationToken);

        return EmptyCommandResponse.Default;
    }
}
