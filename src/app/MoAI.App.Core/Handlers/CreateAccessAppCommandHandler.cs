using MediatR;
using MoAI.App.Commands;
using MoAI.App.Queries.Responses;
using MoAI.App.Services;
using MoAI.Database;
using MoAI.Database.Entities;
using MoAI.Database.Enums;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Helpers;
using MoAI.Team.Services;

namespace MoAI.App.Handlers;

/// <summary>
/// <inheritdoc cref="CreateAccessAppCommand"/>
/// </summary>
public class CreateAccessAppCommandHandler : IRequestHandler<CreateAccessAppCommand, CreateAccessAppCommandResponse>
{
    private readonly DatabaseContext _databaseContext;
    private readonly ITeamService _teamService;

    /// <summary>
    /// Initializes a new instance of the <see cref="CreateAccessAppCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    /// <param name="teamService">团队领域服务.</param>
    public CreateAccessAppCommandHandler(DatabaseContext databaseContext, ITeamService teamService)
    {
        _databaseContext = databaseContext;
        _teamService = teamService;
    }

    /// <inheritdoc/>
    public async Task<CreateAccessAppCommandResponse> Handle(CreateAccessAppCommand request, CancellationToken cancellationToken)
    {
        var myRole = await _teamService.GetMyRoleAsync(request.TeamId, request.ContextUserId, cancellationToken);

        if (myRole == null)
        {
            throw new BusinessException("团队不存在或你不是团队成员.") { StatusCode = 404 };
        }

        if (myRole == TeamRole.Member)
        {
            throw new BusinessException("只有团队管理员可以管理应用接入.") { StatusCode = 403 };
        }

        var (accessAppKey, keyPrefix) = AccessAppKeyGenerator.New();

        // appid（key）列内明文存储；密钥只存 PBKDF2 哈希，原文仅在创建结果中返回一次；
        // 团队接入已去范围化：moai-ac- 全量访问 /api/teamres，不再勾选范围
        var secret = AccessAppKeyGenerator.NewSecret();
        var (secretHash, secretSalt) = PBKDF2Helper.ToHash(secret);
        var entity = new AccessAppEntity
        {
            Id = Guid.CreateVersion7(),
            TeamId = (int)request.TeamId,
            Level = AccessAppLevel.Team,
            Name = request.Name,
            Description = request.Description ?? string.Empty,
            Key = accessAppKey,
            KeyPrefix = keyPrefix,
            Secret = secretHash,
            SecretSalt = secretSalt,
            Status = AccessAppStatus.Enabled,
        };

        _databaseContext.AccessApps.Add(entity);
        await _databaseContext.SaveChangesAsync(cancellationToken);

        return new CreateAccessAppCommandResponse
        {
            AccessAppId = entity.Id,
            Key = accessAppKey,
            Secret = secret,
            KeyPrefix = keyPrefix,
        };
    }
}
