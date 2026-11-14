using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.Account.Services;
using MoAI.Database;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Models;
using MoAI.TeamAgent.Commands;
using MoAI.TeamAgent.Services;

namespace MoAI.TeamAgent.Handlers;

/// <summary>
/// <inheritdoc cref="SeedTeamAgentBuiltinCommand"/>
/// </summary>
public class SeedTeamAgentBuiltinCommandHandler : IRequestHandler<SeedTeamAgentBuiltinCommand, SimpleInt>
{
    private readonly DatabaseContext _databaseContext;
    private readonly IUserAccountService _userAccountService;
    private readonly ITeamAgentSeeder _teamAgentSeeder;

    /// <summary>
    /// Initializes a new instance of the <see cref="SeedTeamAgentBuiltinCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    /// <param name="userAccountService">用户账号领域服务.</param>
    /// <param name="teamAgentSeeder">内置智能体种子服务.</param>
    public SeedTeamAgentBuiltinCommandHandler(DatabaseContext databaseContext, IUserAccountService userAccountService, ITeamAgentSeeder teamAgentSeeder)
    {
        _databaseContext = databaseContext;
        _userAccountService = userAccountService;
        _teamAgentSeeder = teamAgentSeeder;
    }

    /// <inheritdoc/>
    public async Task<SimpleInt> Handle(SeedTeamAgentBuiltinCommand request, CancellationToken cancellationToken)
    {
        var userState = await _userAccountService.GetUserStateAsync(request.ContextUserId, cancellationToken);
        if (!userState.IsAdmin)
        {
            throw new BusinessException("只有管理员可以补种内置智能体.") { StatusCode = 403 };
        }

        var teamIds = await _databaseContext.Teams
            .AsNoTracking()
            .Select(x => (long)x.Id)
            .ToListAsync(cancellationToken);

        var seeded = 0;
        foreach (var teamId in teamIds)
        {
            seeded += await _teamAgentSeeder.EnsureSeededAsync(teamId, cancellationToken);
        }

        return new SimpleInt { Value = seeded };
    }
}
