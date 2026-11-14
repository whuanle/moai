using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.Database;
using MoAI.Database.Entities;
using MoAI.Database.Enums;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Models;
using MoAI.Team.Commands;
using MoAI.Team.Services;

namespace MoAI.Team.Handlers;

/// <summary>
/// <inheritdoc cref="SystemCreateTeamCommand"/>
/// </summary>
public class SystemCreateTeamCommandHandler : IRequestHandler<SystemCreateTeamCommand, SimpleLong>
{
    private readonly DatabaseContext _databaseContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="SystemCreateTeamCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    public SystemCreateTeamCommandHandler(DatabaseContext databaseContext)
    {
        _databaseContext = databaseContext;
    }

    /// <inheritdoc/>
    public async Task<SimpleLong> Handle(SystemCreateTeamCommand request, CancellationToken cancellationToken)
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

        return new SimpleLong { Value = team.Id };
    }
}

/// <summary>
/// <inheritdoc cref="SystemAddTeamUserCommand"/>
/// </summary>
public class SystemAddTeamUserCommandHandler : IRequestHandler<SystemAddTeamUserCommand, EmptyCommandResponse>
{
    private readonly DatabaseContext _databaseContext;
    private readonly ITeamService _teamService;

    /// <summary>
    /// Initializes a new instance of the <see cref="SystemAddTeamUserCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    /// <param name="teamService">团队领域服务.</param>
    public SystemAddTeamUserCommandHandler(DatabaseContext databaseContext, ITeamService teamService)
    {
        _databaseContext = databaseContext;
        _teamService = teamService;
    }

    /// <inheritdoc/>
    public async Task<EmptyCommandResponse> Handle(SystemAddTeamUserCommand request, CancellationToken cancellationToken)
    {
        var teamExist = await _databaseContext.Teams
            .AnyAsync(x => x.Id == request.TeamId, cancellationToken);
        if (!teamExist)
        {
            throw new BusinessException("团队不存在.") { StatusCode = 404 };
        }

        var targetUserExist = await _databaseContext.Users
            .AnyAsync(x => x.Id == request.UserId, cancellationToken);
        if (!targetUserExist)
        {
            throw new BusinessException("用户不存在.") { StatusCode = 404 };
        }

        // 幂等 upsert：已是成员则整体更新角色，适配外部系统重复推送
        var member = await _databaseContext.TeamUsers
            .FirstOrDefaultAsync(x => x.TeamId == request.TeamId && x.UserId == request.UserId, cancellationToken);

        if (member == null)
        {
            _databaseContext.TeamUsers.Add(new TeamUserEntity
            {
                TeamId = (int)request.TeamId,
                UserId = request.UserId,
                Role = (int)request.Role,
            });
        }
        else
        {
            if ((TeamRole)member.Role == TeamRole.Owner && request.Role != TeamRole.Owner)
            {
                throw new BusinessException("团队 Owner 角色不可经系统接口调整.") { StatusCode = 403 };
            }

            member.Role = (int)request.Role;
        }

        await _databaseContext.SaveChangesAsync(cancellationToken);

        await _teamService.RemoveRoleCacheAsync(request.TeamId, request.UserId, cancellationToken);

        return EmptyCommandResponse.Default;
    }
}

/// <summary>
/// <inheritdoc cref="SystemRemoveTeamUserCommand"/>
/// </summary>
public class SystemRemoveTeamUserCommandHandler : IRequestHandler<SystemRemoveTeamUserCommand, EmptyCommandResponse>
{
    private readonly DatabaseContext _databaseContext;
    private readonly ITeamService _teamService;

    /// <summary>
    /// Initializes a new instance of the <see cref="SystemRemoveTeamUserCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    /// <param name="teamService">团队领域服务.</param>
    public SystemRemoveTeamUserCommandHandler(DatabaseContext databaseContext, ITeamService teamService)
    {
        _databaseContext = databaseContext;
        _teamService = teamService;
    }

    /// <inheritdoc/>
    public async Task<EmptyCommandResponse> Handle(SystemRemoveTeamUserCommand request, CancellationToken cancellationToken)
    {
        var member = await _databaseContext.TeamUsers
            .FirstOrDefaultAsync(x => x.TeamId == request.TeamId && x.UserId == request.UserId, cancellationToken);

        if (member == null)
        {
            throw new BusinessException("该用户不是团队成员.") { StatusCode = 404 };
        }

        if ((TeamRole)member.Role == TeamRole.Owner)
        {
            throw new BusinessException("团队 Owner 不可经系统接口移除，请先在团队内转让 Owner.") { StatusCode = 403 };
        }

        _databaseContext.TeamUsers.Remove(member);
        await _databaseContext.SaveChangesAsync(cancellationToken);

        await _teamService.RemoveRoleCacheAsync(request.TeamId, request.UserId, cancellationToken);

        return EmptyCommandResponse.Default;
    }
}
