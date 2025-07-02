using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.Database;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Models;
using MoAI.Login.Commands;

namespace MoAI.Login.Handlers;

/// <summary>
/// <inheritdoc cref="DeleteOAuthConnectionCommand"/>
/// </summary>
public class DeleteOAuthConnectionCommandHandler : IRequestHandler<DeleteOAuthConnectionCommand, EmptyCommandResponse>
{
    private readonly DatabaseContext _databaseContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="DeleteOAuthConnectionCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext"></param>
    public DeleteOAuthConnectionCommandHandler(DatabaseContext databaseContext)
    {
        _databaseContext = databaseContext;
    }

    /// <inheritdoc/>
    public async Task<EmptyCommandResponse> Handle(DeleteOAuthConnectionCommand request, CancellationToken cancellationToken)
    {
        var connection = await _databaseContext.OauthConnections
            .FirstOrDefaultAsync(x => x.Id == request.OAuthConnectionId, cancellationToken);

        if (connection == null)
        {
            throw new BusinessException("未找到认证方式，请检查名称是否正确.");
        }

        _databaseContext.OauthConnections.Remove(connection);
        await _databaseContext.SaveChangesAsync(cancellationToken);

        return EmptyCommandResponse.Default;
    }
}