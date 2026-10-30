using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.Database;
using MoAI.Database.Enums;
using MoAI.Infra.Exceptions;
using MoAI.Subsystem.Commands;
using MoAI.Subsystem.Core.Services;
using MoAI.Subsystem.Queries.Responses;

namespace MoAI.Subsystem.Handlers;

/// <summary>
/// <inheritdoc cref="SubsystemSsoAuthorizeCommand"/>
/// </summary>
public class SubsystemSsoAuthorizeCommandHandler : IRequestHandler<SubsystemSsoAuthorizeCommand, SubsystemSsoAuthorizeCommandResponse>
{
    private readonly DatabaseContext _databaseContext;
    private readonly ISubsystemSsoCodeStore _codeStore;

    /// <summary>
    /// Initializes a new instance of the <see cref="SubsystemSsoAuthorizeCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    /// <param name="codeStore">委托码存储.</param>
    public SubsystemSsoAuthorizeCommandHandler(DatabaseContext databaseContext, ISubsystemSsoCodeStore codeStore)
    {
        _databaseContext = databaseContext;
        _codeStore = codeStore;
    }

    /// <inheritdoc/>
    public async Task<SubsystemSsoAuthorizeCommandResponse> Handle(SubsystemSsoAuthorizeCommand request, CancellationToken cancellationToken)
    {
        var subsystem = await _databaseContext.Subsystems
            .FirstOrDefaultAsync(x => x.Key == request.SubsystemKey, cancellationToken);
        if (subsystem == null)
        {
            throw new BusinessException("子系统不存在.") { StatusCode = 404 };
        }

        if (subsystem.Status == SubsystemStatus.Disabled)
        {
            throw new BusinessException("子系统已停用.") { StatusCode = 403 };
        }

        if (string.IsNullOrWhiteSpace(subsystem.CallbackUrl))
        {
            throw new BusinessException("该子系统未配置登录回调地址，暂不支持委托登录.") { StatusCode = 403 };
        }

        // 团队授权门禁：全员范围要求用户至少加入一个团队；指定范围要求用户团队与授权清单有交集
        var userTeamIds = await _databaseContext.TeamUsers
            .Where(x => x.UserId == request.ContextUserId)
            .Select(x => x.TeamId)
            .ToListAsync(cancellationToken);
        if (subsystem.TeamScope == SubsystemTeamScope.SpecifiedTeams)
        {
            var authorizedTeamIds = await _databaseContext.SubsystemTeamAuths
                .Where(x => x.SubsystemId == subsystem.Id)
                .Select(x => x.TeamId)
                .ToListAsync(cancellationToken);
            if (!userTeamIds.Intersect(authorizedTeamIds).Any())
            {
                throw new BusinessException("你所在的团队未获授权使用该子系统.") { StatusCode = 403 };
            }
        }
        else if (userTeamIds.Count == 0)
        {
            throw new BusinessException("你尚未加入任何团队，无法使用该子系统.") { StatusCode = 403 };
        }

        var code = await _codeStore.CreateAsync(subsystem.Id, request.ContextUserId);
        return new SubsystemSsoAuthorizeCommandResponse
        {
            Code = code,
            CallbackUrl = subsystem.CallbackUrl,
            Name = subsystem.Name,
        };
    }
}
