using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.App.Commands;
using MoAI.Database;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Models;

namespace MoAI.App.Handlers;

/// <summary>
/// <inheritdoc cref="UpdateAppSessionTitleCommand"/>
/// </summary>
public class UpdateAppSessionTitleCommandHandler : IRequestHandler<UpdateAppSessionTitleCommand, EmptyCommandResponse>
{
    private readonly DatabaseContext _databaseContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="UpdateAppSessionTitleCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    public UpdateAppSessionTitleCommandHandler(DatabaseContext databaseContext)
    {
        _databaseContext = databaseContext;
    }

    /// <inheritdoc/>
    public async Task<EmptyCommandResponse> Handle(UpdateAppSessionTitleCommand request, CancellationToken cancellationToken)
    {
        var session = await _databaseContext.AppAgentSessions
            .FirstOrDefaultAsync(x => x.Id == request.SessionId, cancellationToken);

        if (session == null || session.CreateUserId != request.ContextUserId)
        {
            throw new BusinessException("会话不存在.") { StatusCode = 404 };
        }

        session.Title = request.Title;
        await _databaseContext.SaveChangesAsync(cancellationToken);

        return EmptyCommandResponse.Default;
    }
}
