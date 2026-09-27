using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.App.Commands;
using MoAI.Database;
using MoAI.Database.Enums;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Models;
using MoAI.Gateway.Services;
using MoAI.Team.Services;

namespace MoAI.App.Handlers;

/// <summary>
/// <inheritdoc cref="DeleteAccessAppCommand"/>
/// </summary>
public class DeleteAccessAppCommandHandler : IRequestHandler<DeleteAccessAppCommand, EmptyCommandResponse>
{
    private readonly DatabaseContext _databaseContext;
    private readonly ITeamService _teamService;
    private readonly ExternalKeyCache _keyCache;

    /// <summary>
    /// Initializes a new instance of the <see cref="DeleteAccessAppCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    /// <param name="teamService">团队领域服务.</param>
    /// <param name="keyCache">外部接入 key 缓存.</param>
    public DeleteAccessAppCommandHandler(DatabaseContext databaseContext, ITeamService teamService, ExternalKeyCache keyCache)
    {
        _databaseContext = databaseContext;
        _teamService = teamService;
        _keyCache = keyCache;
    }

    /// <inheritdoc/>
    public async Task<EmptyCommandResponse> Handle(DeleteAccessAppCommand request, CancellationToken cancellationToken)
    {
        var entity = await _databaseContext.AccessApps.FirstOrDefaultAsync(x => x.Id == request.AccessAppId, cancellationToken);

        if (entity == null)
        {
            throw new BusinessException("应用接入不存在.") { StatusCode = 404 };
        }

        var myRole = await _teamService.GetMyRoleAsync(entity.TeamId, request.ContextUserId, cancellationToken);

        if (myRole == null)
        {
            throw new BusinessException("团队不存在或你不是团队成员.") { StatusCode = 404 };
        }

        if (myRole == TeamRole.Member)
        {
            throw new BusinessException("只有团队管理员可以管理应用接入.") { StatusCode = 403 };
        }

        await _databaseContext.SoftDeleteAsync(_databaseContext.AccessApps.Where(x => x.Id == entity.Id));

        // 软删除后删除缓存，key 直连立即失效
        await _keyCache.RemoveAccessKeyAsync(ApiKeyGenerator.Hash(entity.Key));

        return EmptyCommandResponse.Default;
    }
}
