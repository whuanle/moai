using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.Database;
using MoAI.Gateway.Commands;
using MoAI.Gateway.Queries.Responses;
using MoAI.Gateway.Services;
using MoAI.Infra.Exceptions;

namespace MoAI.Gateway.Handlers;

/// <summary>
/// 创建团队网关 API Key.
/// </summary>
public class CreateTeamApiKeyCommandHandler : IRequestHandler<CreateTeamApiKeyCommand, CreateTeamApiKeyCommandResponse>
{
    private readonly DatabaseContext _databaseContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="CreateTeamApiKeyCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    public CreateTeamApiKeyCommandHandler(DatabaseContext databaseContext)
    {
        _databaseContext = databaseContext;
    }

    /// <inheritdoc/>
    public async Task<CreateTeamApiKeyCommandResponse> Handle(CreateTeamApiKeyCommand request, CancellationToken cancellationToken)
    {
        var teamExist = await _databaseContext.Teams.AnyAsync(x => x.Id == request.TeamId, cancellationToken);
        if (!teamExist)
        {
            throw new BusinessException("团队不存在.") { StatusCode = 404 };
        }

        var (secret, keyPrefix, sha256) = ApiKeyGenerator.New();

        var entity = new Database.Entities.TeamApiKeyEntity
        {
            TeamId = request.TeamId,
            Name = request.Name,
            KeyPrefix = keyPrefix,
            KeySha256 = sha256,
            CreatorUserId = request.ContextUserId,
            IsDisable = false,
            ExpireTime = request.ExpireTime ?? ApiKeySentinels.NeverExpire,
            LastUsedTime = ApiKeySentinels.NeverUsed,
        };

        _databaseContext.TeamApiKeys.Add(entity);
        await _databaseContext.SaveChangesAsync(cancellationToken);

        return new CreateTeamApiKeyCommandResponse
        {
            ApiKeyId = entity.Id,
            Secret = secret,
            KeyPrefix = keyPrefix,
        };
    }
}
