using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.App.Commands;
using MoAI.Database;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Models;

namespace MoAI.App.Handlers;

/// <summary>
/// <inheritdoc cref="DeleteAppSessionCommand"/>
/// </summary>
public class DeleteAppSessionCommandHandler : IRequestHandler<DeleteAppSessionCommand, EmptyCommandResponse>
{
    private readonly DatabaseContext _databaseContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="DeleteAppSessionCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    public DeleteAppSessionCommandHandler(DatabaseContext databaseContext)
    {
        _databaseContext = databaseContext;
    }

    /// <inheritdoc/>
    public async Task<EmptyCommandResponse> Handle(DeleteAppSessionCommand request, CancellationToken cancellationToken)
    {
        var session = await _databaseContext.AppAgentSessions
            .FirstOrDefaultAsync(x => x.Id == request.SessionId, cancellationToken);

        if (session == null || session.CreateUserId != request.ContextUserId)
        {
            throw new BusinessException("会话不存在.") { StatusCode = 404 };
        }

        await _databaseContext.SoftDeleteAsync(_databaseContext.AppAgentMessages.Where(x => x.SessionId == session.Id));
        await _databaseContext.SoftDeleteAsync(_databaseContext.AppAgentSessions.Where(x => x.Id == session.Id));

        return EmptyCommandResponse.Default;
    }
}
