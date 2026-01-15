using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.Database;
using MoAI.Infra.Models;
using MoAI.Login.Commands;

namespace MoAI.Admin.User.Commands;

/// <summary>
/// <inheritdoc cref="SetUserAdminCommand"/>
/// </summary>
public class SetUserAdminCommandHandler : IRequestHandler<SetUserAdminCommand, EmptyCommandResponse>
{
    private readonly DatabaseContext _databaseContext;
    private readonly IMediator _mediator;

    /// <summary>
    /// Initializes a new instance of the <see cref="SetUserAdminCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext"></param>
    /// <param name="mediator"></param>
    public SetUserAdminCommandHandler(DatabaseContext databaseContext, IMediator mediator)
    {
        _databaseContext = databaseContext;
        _mediator = mediator;
    }

    /// <inheritdoc/>
    public async Task<EmptyCommandResponse> Handle(SetUserAdminCommand request, CancellationToken cancellationToken)
    {
        await _databaseContext.WhereUpdateAsync(
            _databaseContext.Users.Where(x => request.UserIds.Contains(x.Id)),
            x => x.SetProperty(a => a.IsAdmin, request.IsAdmin));

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
