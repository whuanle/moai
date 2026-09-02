using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.Database;
using MoAI.Database.Entities;
using MoAI.Database.Enums;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Models;
using MoAI.Infra.Services;
using MoAI.Team.Commands;
using MoAI.Team.Services;

namespace MoAI.Team.Handlers;

/// <summary>
/// <inheritdoc cref="AddTeamUserCommand"/>
/// </summary>
public class AddTeamUserCommandHandler : IRequestHandler<AddTeamUserCommand, EmptyCommandResponse>
{
    private readonly DatabaseContext _databaseContext;
    private readonly ITeamService _teamService;
    private readonly IUserContextProvider _userContextProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="AddTeamUserCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    /// <param name="teamService">团队领域服务.</param>
    /// <param name="userContextProvider">用户上下文提供者.</param>
    public AddTeamUserCommandHandler(DatabaseContext databaseContext, ITeamService teamService, IUserContextProvider userContextProvider)
    {
        _databaseContext = databaseContext;
        _teamService = teamService;
        _userContextProvider = userContextProvider;
    }

    /// <inheritdoc/>
    public async Task<EmptyCommandResponse> Handle(AddTeamUserCommand request, CancellationToken cancellationToken)
    {
        var userId = _userContextProvider.GetUserContext().UserId;
        var myRole = await _teamService.GetMyRoleAsync(request.TeamId, userId, cancellationToken);

        if (myRole == null || myRole == TeamRole.Member)
        {
            throw new BusinessException("只有团队管理员可以添加成员.") { StatusCode = myRole == null ? 404 : 403 };
        }

        if (request.Role == TeamRole.Admin && myRole != TeamRole.Owner)
        {
            throw new BusinessException("只有团队所有者可以授予管理员角色.") { StatusCode = 403 };
        }

        var targetRole = await _teamService.GetMyRoleAsync(request.TeamId, request.UserId, cancellationToken);

        if (targetRole != null)
        {
            throw new BusinessException("该用户已经是团队成员.") { StatusCode = 409 };
        }

        var targetUserExist = await _databaseContext.Users
            .AnyAsync(x => x.Id == request.UserId, cancellationToken);

        if (!targetUserExist)
        {
            throw new BusinessException("用户不存在.") { StatusCode = 404 };
        }

        _databaseContext.TeamUsers.Add(new TeamUserEntity
        {
            TeamId = request.TeamId,
            UserId = request.UserId,
            Role = request.Role,
        });

        await _databaseContext.SaveChangesAsync(cancellationToken);

        return EmptyCommandResponse.Default;
    }
}
