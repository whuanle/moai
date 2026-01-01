using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.Database;
using MoAI.Database.Entities;
using MoAI.Database.Helper;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Models;
using MoAI.Team.Commands;
using MoAI.Team.Models;

namespace MoAI.Team.Handlers;

/// <summary>
/// <inheritdoc cref="CreateTeamCommand"/>
/// </summary>
public class CreateTeamCommandHandler : IRequestHandler<CreateTeamCommand, CreateTeamCommandResponse>
{
    private readonly DatabaseContext _databaseContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="CreateTeamCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext"></param>
    public CreateTeamCommandHandler(DatabaseContext databaseContext)
    {
        _databaseContext = databaseContext;
    }

    /// <inheritdoc/>
    public async Task<CreateTeamCommandResponse> Handle(CreateTeamCommand request, CancellationToken cancellationToken)
    {
        // 检查团队名称是否已存在
        var exist = await _databaseContext.Teams.AnyAsync(x => x.Name == request.Name, cancellationToken);
        if (exist)
        {
            throw new BusinessException("已存在同名团队") { StatusCode = 409 };
        }

        var teamEntity = new TeamEntity
        {
            Name = request.Name,
            Description = request.Description ?? string.Empty,
            Avatar = request.Avatar ?? string.Empty,
        };

        using var tran = TransactionScopeHelper.Create();

        await _databaseContext.Teams.AddAsync(teamEntity, cancellationToken);
        await _databaseContext.SaveChangesAsync(cancellationToken);

        // 创建者自动成为所有者
        var teamUserEntity = new TeamUserEntity
        {
            TeamId = teamEntity.Id,
            UserId = request.ContextUserId,
            Role = (int)TeamRole.Owner
        };

        await _databaseContext.TeamUsers.AddAsync(teamUserEntity, cancellationToken);
        await _databaseContext.SaveChangesAsync(cancellationToken);

        tran.Complete();

        return new CreateTeamCommandResponse { TeamId = teamEntity.Id };
    }
}
