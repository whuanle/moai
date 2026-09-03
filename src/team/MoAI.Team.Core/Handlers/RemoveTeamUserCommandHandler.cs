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
/// <inheritdoc cref="RemoveTeamUserCommand"/>
/// <para>
/// 权限规则：Owner 可移除 Admin/Member；Admin 仅可移除 Member；成员可自行退出；Owner 不可被移除、不可退出（需先转让或解散）.
/// </para>
/// </summary>
public class RemoveTeamUserCommandHandler : IRequestHandler<RemoveTeamUserCommand, EmptyCommandResponse>
{
    private readonly DatabaseContext _databaseContext;
    private readonly ITeamService _teamService;
    private readonly IUserContextProvider _userContextProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="RemoveTeamUserCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    /// <param name="teamService">团队领域服务.</param>
    /// <param name="userContextProvider">用户上下文提供者.</param>
    public RemoveTeamUserCommandHandler(DatabaseContext databaseContext, ITeamService teamService, IUserContextProvider userContextProvider)
    {
        _databaseContext = databaseContext;
        _teamService = teamService;
        _userContextProvider = userContextProvider;
    }

    /// <inheritdoc/>
    public async Task<EmptyCommandResponse> Handle(RemoveTeamUserCommand request, CancellationToken cancellationToken)
    {
        var userId = _userContextProvider.GetUserContext().UserId;
        var myRole = await _teamService.GetMyRoleAsync(request.TeamId, userId, cancellationToken);

        if (myRole == null)
        {
            throw new BusinessException("团队不存在或你不是团队成员.") { StatusCode = 404 };
        }

        var targetRole = await _teamService.GetMyRoleAsync(request.TeamId, request.UserId, cancellationToken);

        if (targetRole == null)
        {
            throw new BusinessException("目标用户不是团队成员.") { StatusCode = 404 };
        }

        if (targetRole == TeamRole.Owner)
        {
            throw new BusinessException("不能移除团队所有者.") { StatusCode = 400 };
        }

        var isSelfLeave = request.UserId == userId;
        if (isSelfLeave && myRole == TeamRole.Owner)
        {
            throw new BusinessException("团队所有者不能退出团队，请先解散团队.") { StatusCode = 400 };
        }

        if (!isSelfLeave)
        {
            if (myRole == TeamRole.Member)
            {
                throw new BusinessException("只有团队管理员可以移除成员.") { StatusCode = 403 };
            }

            if (myRole == TeamRole.Admin && targetRole == TeamRole.Admin)
            {
                throw new BusinessException("管理员不能移除其他管理员.") { StatusCode = 403 };
            }
        }

        var member = await _databaseContext.TeamUsers
            .FirstAsync(x => x.TeamId == request.TeamId && x.UserId == request.UserId, cancellationToken);

        _databaseContext.TeamUsers.Remove(member);
        await _databaseContext.SaveChangesAsync(cancellationToken);

        return EmptyCommandResponse.Default;
    }
}
