using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.Database;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Models;
using MoAI.Team.Commands;
using MoAI.Team.Models;

namespace MoAI.Team.Handlers;

/// <summary>
/// <inheritdoc cref="TransferTeamOwnerCommand"/>
/// </summary>
public class TransferTeamOwnerCommandHandler : IRequestHandler<TransferTeamOwnerCommand, EmptyCommandResponse>
{
    private readonly DatabaseContext _databaseContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="TransferTeamOwnerCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext"></param>
    public TransferTeamOwnerCommandHandler(DatabaseContext databaseContext)
    {
        _databaseContext = databaseContext;
    }

    /// <inheritdoc/>
    public async Task<EmptyCommandResponse> Handle(TransferTeamOwnerCommand request, CancellationToken cancellationToken)
    {
        // 检查新所有者是否是团队成员
        var owner = await _databaseContext.TeamUsers
            .Where(x => x.TeamId == request.TeamId && x.Role == (int)TeamRole.Owner)
            .FirstOrDefaultAsync(cancellationToken);
        if (owner == null)
        {
            throw new BusinessException("当前团队没有所有者") { StatusCode = 404 };
        }

        var newOwner = await _databaseContext.TeamUsers
        .Where(x => x.TeamId == request.TeamId && x.UserId == request.NewOwnerUserId)
        .FirstOrDefaultAsync(cancellationToken);

        if (newOwner == null)
        {
            throw new BusinessException("新所有者不是团队成员") { StatusCode = 404 };
        }

        // 转让所有权
        owner.Role = (int)TeamRole.Admin;
        newOwner.Role = (int)TeamRole.Owner;

        _databaseContext.UpdateRange(owner, newOwner);
        await _databaseContext.SaveChangesAsync(cancellationToken);

        return EmptyCommandResponse.Default;
    }
}
