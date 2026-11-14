using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MoAI.Database;
using MoAI.Database.Entities;
using MoAI.Database.Enums;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Models;
using MoAI.Team.Commands;
using MoAI.TeamAgent.Services;

namespace MoAI.Team.Handlers;

/// <summary>
/// <inheritdoc cref="CreateTeamCommand"/>
/// </summary>
public class CreateTeamCommandHandler : IRequestHandler<CreateTeamCommand, SimpleLong>
{
    private readonly DatabaseContext _databaseContext;
    private readonly ITeamAgentSeeder _teamAgentSeeder;
    private readonly ILogger<CreateTeamCommandHandler> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="CreateTeamCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    /// <param name="teamAgentSeeder">内置智能体种子服务.</param>
    /// <param name="logger">日志.</param>
    public CreateTeamCommandHandler(DatabaseContext databaseContext, ITeamAgentSeeder teamAgentSeeder, ILogger<CreateTeamCommandHandler> logger)
    {
        _databaseContext = databaseContext;
        _teamAgentSeeder = teamAgentSeeder;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<SimpleLong> Handle(CreateTeamCommand request, CancellationToken cancellationToken)
    {
        var exist = await _databaseContext.Teams
            .AnyAsync(x => x.Name == request.Name, cancellationToken);

        if (exist)
        {
            throw new BusinessException("团队名称已存在，请更换后重试.") { StatusCode = 409 };
        }

        var team = new TeamEntity
        {
            Name = request.Name,
            Description = request.Description ?? string.Empty,
            AvatarPath = string.Empty,
        };

        _databaseContext.Teams.Add(team);
        await _databaseContext.SaveChangesAsync(cancellationToken);

        _databaseContext.TeamUsers.Add(new TeamUserEntity
        {
            TeamId = team.Id,
            UserId = request.ContextUserId,
            Role = (int)TeamRole.Owner,
        });

        await _databaseContext.SaveChangesAsync(cancellationToken);

        // 种入系统内置智能体（规划师/构建师）；失败不阻断团队创建，存量补种走 /api/team-agent/seed-builtin
        try
        {
            await _teamAgentSeeder.EnsureSeededAsync(team.Id, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "团队 {TeamId} 内置智能体种入失败，可通过 seed-builtin 端点补种.", team.Id);
        }

        return new SimpleLong
        {
            Value = team.Id
        };
    }
}
