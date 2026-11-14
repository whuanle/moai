using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.Account.Services;
using MoAI.Database;
using MoAI.Database.Entities;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Models;
using MoAI.Team.Services;
using MoAI.TeamAgent.Commands;
using MoAI.TeamAgent.Services;

namespace MoAI.TeamAgent.Handlers;

/// <summary>
/// <inheritdoc cref="CreateTeamAgentCommand"/>
/// </summary>
public class CreateTeamAgentCommandHandler : IRequestHandler<CreateTeamAgentCommand, SimpleGuid>
{
    private readonly DatabaseContext _databaseContext;
    private readonly IUserAccountService _userAccountService;
    private readonly ITeamService _teamService;

    /// <summary>
    /// Initializes a new instance of the <see cref="CreateTeamAgentCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    /// <param name="userAccountService">用户账号领域服务.</param>
    /// <param name="teamService">团队领域服务.</param>
    public CreateTeamAgentCommandHandler(DatabaseContext databaseContext, IUserAccountService userAccountService, ITeamService teamService)
    {
        _databaseContext = databaseContext;
        _userAccountService = userAccountService;
        _teamService = teamService;
    }

    /// <inheritdoc/>
    public async Task<SimpleGuid> Handle(CreateTeamAgentCommand request, CancellationToken cancellationToken)
    {
        await TeamAgentAccessGuard.EnsureCanManageAsync(request.TeamId, request.ContextUserId, _userAccountService, _teamService, cancellationToken);

        var nameExist = await _databaseContext.TeamAgents
            .AnyAsync(x => x.TeamId == request.TeamId && x.Name == request.Name, cancellationToken);

        if (nameExist)
        {
            throw new BusinessException("智能体名称已存在，请更换后重试.") { StatusCode = 409 };
        }

        var keyExist = await _databaseContext.TeamAgents
            .AnyAsync(x => x.TeamId == request.TeamId && x.Key == request.Key, cancellationToken);

        if (keyExist)
        {
            throw new BusinessException("智能体标识已存在，请更换后重试.") { StatusCode = 409 };
        }

        await TeamAgentCommandHelper.EnsureAvatarValidAsync(_databaseContext, request.Avatar, cancellationToken);

        var (mainPluginIds, mainWikiIds, mainGraphIds, subs) = await TeamAgentCommandHelper.NormalizeAsync(
            _databaseContext, request.TeamId, request.MainPluginIds, request.MainWikiIds, request.MainGraphIds, request.Subs, cancellationToken);

        var agent = new TeamAgentEntity
        {
            Id = Guid.CreateVersion7(),
            TeamId = request.TeamId,
            Key = request.Key,
            Name = request.Name,
            Description = request.Description ?? string.Empty,
            AvatarObjectKey = request.Avatar ?? string.Empty,
            Enabled = request.Enabled,
            MainPrompt = request.MainPrompt,
            MainPluginIds = TeamAgentJson.SerializePluginIds(mainPluginIds),
            MainWikiIds = TeamAgentJson.SerializeWikiIds(mainWikiIds),
            MainGraphIds = TeamAgentJson.SerializeWikiIds(mainGraphIds),
            Subs = TeamAgentJson.SerializeSubs(subs),
        };

        _databaseContext.TeamAgents.Add(agent);
        await _databaseContext.SaveChangesAsync(cancellationToken);

        return new SimpleGuid { Value = agent.Id };
    }
}
