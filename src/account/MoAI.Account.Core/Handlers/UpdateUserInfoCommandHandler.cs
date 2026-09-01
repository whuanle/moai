using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.Account.Commands;
using MoAI.Account.Services;
using MoAI.Database;
using MoAI.Database.Entities;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Models;

namespace MoAI.Account.Handlers;

/// <summary>
/// <inheritdoc cref="UpdateUserInfoCommand"/>
/// </summary>
public class UpdateUserInfoCommandHandler : IRequestHandler<UpdateUserInfoCommand, EmptyCommandResponse>
{
    private readonly DatabaseContext _databaseContext;
    private readonly IUserAccountService _userAccountService;

    /// <summary>
    /// Initializes a new instance of the <see cref="UpdateUserInfoCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext"></param>
    /// <param name="userAccountService"></param>
    public UpdateUserInfoCommandHandler(DatabaseContext databaseContext, IUserAccountService userAccountService)
    {
        _databaseContext = databaseContext;
        _userAccountService = userAccountService;
    }

    /// <inheritdoc/>
    public async Task<EmptyCommandResponse> Handle(UpdateUserInfoCommand request, CancellationToken cancellationToken)
    {
        var user = await _databaseContext.Users.FirstOrDefaultAsync(x => x.Id == request.ContextUserId, cancellationToken);
        if (user == null)
        {
            throw new BusinessException("用户不存在") { StatusCode = 404 };
        }

        if (!string.IsNullOrWhiteSpace(request.NickName))
        {
            user.NickName = request.NickName;
        }

        if (request.Phone != null)
        {
            user.Phone = request.Phone;
        }

        _databaseContext.Users.Update(user);
        await _databaseContext.SaveChangesAsync(cancellationToken);

        await _userAccountService.RemoveUserStateAsync(request.ContextUserId, cancellationToken);

        return EmptyCommandResponse.Default;
    }
}
