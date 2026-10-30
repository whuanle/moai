using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.Database;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Helpers;
using MoAI.Infra.Models;
using MoAI.Subsystem.Commands;
using MoAI.Subsystem.Queries.Responses;
using MoAI.Subsystem.Services;

namespace MoAI.Subsystem.Handlers;

/// <summary>
/// <inheritdoc cref="ResetSubsystemSecretCommand"/>
/// </summary>
public class ResetSubsystemSecretCommandHandler : IRequestHandler<ResetSubsystemSecretCommand, ResetSubsystemSecretCommandResponse>
{
    private readonly DatabaseContext _databaseContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="ResetSubsystemSecretCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    public ResetSubsystemSecretCommandHandler(DatabaseContext databaseContext)
    {
        _databaseContext = databaseContext;
    }

    /// <inheritdoc/>
    public async Task<ResetSubsystemSecretCommandResponse> Handle(ResetSubsystemSecretCommand request, CancellationToken cancellationToken)
    {
        var subsystem = await _databaseContext.Subsystems
            .FirstOrDefaultAsync(x => x.Id == request.SubsystemId, cancellationToken);
        if (subsystem == null)
        {
            throw new BusinessException("子系统不存在.") { StatusCode = 404 };
        }

        var secret = SubsystemSecretGenerator.New();
        var (secretHash, secretSalt) = PBKDF2Helper.ToHash(secret);
        subsystem.Secret = secretHash;
        subsystem.SecretSalt = secretSalt;
        _databaseContext.Subsystems.Update(subsystem);
        await _databaseContext.SaveChangesAsync(cancellationToken);

        return new ResetSubsystemSecretCommandResponse { Secret = secret };
    }
}
