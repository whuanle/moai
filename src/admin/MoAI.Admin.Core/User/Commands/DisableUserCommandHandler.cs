using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.Database;
using MoAI.Infra.Models;
using MoAI.Login.Commands;

namespace MoAI.Admin.User.Commands;

/// <summary>
/// <inheritdoc cref="DisableUserCommand"/>
/// </summary>
public class DisableUserCommandHandler : IRequestHandler<DisableUserCommand, EmptyCommandResponse>
{
    private readonly DatabaseContext _databaseContext;
    private readonly IMediator _mediator;

    /// <summary>
    /// Initializes a new instance of the <see cref="DisableUserCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext"></param>
    /// <param name="mediator"></param>
    public DisableUserCommandHandler(DatabaseContext databaseContext, IMediator mediator)
    {
        _databaseContext = databaseContext;
        _mediator = mediator;
    }

    /// <inheritdoc/>
    public async Task<EmptyCommandResponse> Handle(DisableUserCommand request, CancellationToken cancellationToken)
    {
        await _databaseContext.Users.Where(x => request.UserIds.Contains(x.Id))
            .ExecuteUpdateAsync(x => x.SetProperty(a => a.IsDisable, request.IsDisable));

        foreach (var item in request.UserIds)
        {
            await _mediator.Send(new RefreshUserStateCommand
            {
                UserId = item,
            });
        }

        return EmptyCommandResponse.Default;
    }
}
