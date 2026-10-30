using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.Database;
using MoAI.Database.Entities;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Helpers;
using MoAI.Infra.Models;
using MoAI.Subsystem.Commands;
using MoAI.Subsystem.Queries.Responses;
using MoAI.Subsystem.Services;

namespace MoAI.Subsystem.Handlers;

/// <summary>
/// <inheritdoc cref="CreateSubsystemCommand"/>
/// </summary>
public class CreateSubsystemCommandHandler : IRequestHandler<CreateSubsystemCommand, CreateSubsystemCommandResponse>
{
    private readonly DatabaseContext _databaseContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="CreateSubsystemCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    public CreateSubsystemCommandHandler(DatabaseContext databaseContext)
    {
        _databaseContext = databaseContext;
    }

    /// <inheritdoc/>
    public async Task<CreateSubsystemCommandResponse> Handle(CreateSubsystemCommand request, CancellationToken cancellationToken)
    {
        var keyExists = await _databaseContext.Subsystems.AnyAsync(x => x.Key == request.Key, cancellationToken);
        if (keyExists)
        {
            throw new BusinessException($"子系统标识 {request.Key} 已存在.") { StatusCode = 409 };
        }

        var teamIds = await SubsystemTeamsGuard.NormalizeAsync(request.TeamScope, request.TeamIds, _databaseContext, cancellationToken);

        // 密钥原文仅此一次返回，库内只存 PBKDF2 哈希
        var secret = SubsystemSecretGenerator.New();
        var (secretHash, secretSalt) = PBKDF2Helper.ToHash(secret);

        var entity = new SubsystemEntity
        {
            Id = Guid.CreateVersion7(),
            Key = request.Key,
            Name = request.Name,
            Description = request.Description ?? string.Empty,
            Secret = secretHash,
            SecretSalt = secretSalt,
            Status = Database.Enums.SubsystemStatus.Enabled,
            TeamScope = request.TeamScope,
            CallbackUrl = request.CallbackUrl ?? string.Empty,
        };
        _databaseContext.Subsystems.Add(entity);

        foreach (var teamId in teamIds)
        {
            _databaseContext.SubsystemTeamAuths.Add(new SubsystemTeamAuthEntity
            {
                Id = Guid.CreateVersion7(),
                SubsystemId = entity.Id,
                TeamId = teamId,
            });
        }

        await _databaseContext.SaveChangesAsync(cancellationToken);
        return new CreateSubsystemCommandResponse { SubsystemId = entity.Id, Secret = secret };
    }
}
