using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.Account.Commands;
using MoAI.Database;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Models;

namespace MoAI.Account.Handlers;

/// <summary>
/// <inheritdoc cref="UnbindUserOAuthCommand"/>
/// </summary>
public class UnbindUserOAuthCommandHandler : IRequestHandler<UnbindUserOAuthCommand, EmptyCommandResponse>
{
    private readonly DatabaseContext _databaseContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="UnbindUserOAuthCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext"></param>
    public UnbindUserOAuthCommandHandler(DatabaseContext databaseContext)
    {
        _databaseContext = databaseContext;
    }

    /// <inheritdoc/>
    public async Task<EmptyCommandResponse> Handle(UnbindUserOAuthCommand request, CancellationToken cancellationToken)
    {
        var record = await _databaseContext.UserOauthConnections
            .FirstOrDefaultAsync(x => x.UserId == request.ContextUserId && x.ProviderId == request.ProviderId, cancellationToken);

        if (record == null)
        {
            throw new BusinessException("未绑定该第三方账号") { StatusCode = 404 };
        }

        _databaseContext.UserOauthConnections.Remove(record);
        await _databaseContext.SaveChangesAsync(cancellationToken);

        return EmptyCommandResponse.Default;
    }
}
