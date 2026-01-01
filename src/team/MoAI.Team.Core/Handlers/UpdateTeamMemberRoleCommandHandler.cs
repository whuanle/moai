using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.Database;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Models;
using MoAI.Team.Commands;
using MoAI.Team.Models;

namespace MoAI.Team.Handlers;

/// <summary>
/// <inheritdoc cref="UpdateTeamMemberRoleCommand"/>
/// </summary>
public class UpdateTeamMemberRoleCommandHandler : IRequestHandler<UpdateTeamMemberRoleCommand, EmptyCommandResponse>
{
    private readonly DatabaseContext _databaseContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="UpdateTeamMemberRoleCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext"></param>
    public UpdateTeamMemberRoleCommandHandler(DatabaseContext databaseContext)
    {
        _databaseContext = databaseContext;
    }

    /// <inheritdoc/>
    public async Task<EmptyCommandResponse> Handle(UpdateTeamMemberRoleCommand request, CancellationToken cancellationToken)
    {
        // 获取目标成员
        var targetMember = await _databaseContext.TeamUsers
            .Where(x => x.TeamId == request.TeamId && x.UserId == request.UserId)
            .FirstOrDefaultAsync(cancellationToken);

        if (targetMember == null)
        {
            throw new BusinessException("该用户不是团队成员") { StatusCode = 404 };
        }

        // 不能修改所有者的角色
        if (targetMember.Role == (int)TeamRole.Owner)
        {
            throw new BusinessException("不能修改所有者的角色") { StatusCode = 403 };
        }

        targetMember.Role = (int)request.Role;
        await _databaseContext.SaveChangesAsync(cancellationToken);

        return EmptyCommandResponse.Default;
    }
}
