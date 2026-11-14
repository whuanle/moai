using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.App.Commands;
using MoAI.App.Queries.Responses;
using MoAI.App.Services;
using MoAI.Database;
using MoAI.Database.Enums;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Helpers;
using MoAI.Team.Services;

namespace MoAI.App.Handlers;

/// <summary>
/// <inheritdoc cref="ResetAccessAppSecretCommand"/>
/// </summary>
public class ResetAccessAppSecretCommandHandler : IRequestHandler<ResetAccessAppSecretCommand, ResetAccessAppSecretCommandResponse>
{
    private readonly DatabaseContext _databaseContext;
    private readonly ITeamService _teamService;

    /// <summary>
    /// Initializes a new instance of the <see cref="ResetAccessAppSecretCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    /// <param name="teamService">团队领域服务.</param>
    public ResetAccessAppSecretCommandHandler(DatabaseContext databaseContext, ITeamService teamService)
    {
        _databaseContext = databaseContext;
        _teamService = teamService;
    }

    /// <inheritdoc/>
    public async Task<ResetAccessAppSecretCommandResponse> Handle(ResetAccessAppSecretCommand request, CancellationToken cancellationToken)
    {
        var entity = await _databaseContext.AccessApps
            .Where(x => x.Level == AccessAppLevel.Team)
            .FirstOrDefaultAsync(x => x.Id == request.AccessAppId, cancellationToken);

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

        // 新密钥重新哈希落库，旧密钥立即失效
        var secret = AccessAppKeyGenerator.NewSecret();
        var (secretHash, secretSalt) = PBKDF2Helper.ToHash(secret);
        entity.Secret = secretHash;
        entity.SecretSalt = secretSalt;
        await _databaseContext.SaveChangesAsync(cancellationToken);

        return new ResetAccessAppSecretCommandResponse { Secret = secret };
    }
}
