using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.Account.Commands;
using MoAI.Account.Services;
using MoAI.Database;
using MoAI.Database.Entities;
using MoAI.Infra.Events;
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
    private readonly IAccessEventPublisher _eventPublisher;

    /// <summary>
    /// Initializes a new instance of the <see cref="UpdateUserInfoCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext"></param>
    /// <param name="userAccountService"></param>
    /// <param name="eventPublisher">接入事件发布器.</param>
    public UpdateUserInfoCommandHandler(DatabaseContext databaseContext, IUserAccountService userAccountService, IAccessEventPublisher eventPublisher)
    {
        _databaseContext = databaseContext;
        _userAccountService = userAccountService;
        _eventPublisher = eventPublisher;
    }

    /// <inheritdoc/>
    public async Task<EmptyCommandResponse> Handle(UpdateUserInfoCommand request, CancellationToken cancellationToken)
    {
        var user = await _databaseContext.Users.FirstOrDefaultAsync(x => x.Id == request.ContextUserId, cancellationToken);
        if (user == null)
        {
            throw new BusinessException("用户不存在") { StatusCode = 404 };
        }

        var fields = new List<string>();
        if (!string.IsNullOrWhiteSpace(request.NickName) && user.NickName != request.NickName)
        {
            user.NickName = request.NickName;
            fields.Add("nickName");
        }

        if (request.Phone != null && user.Phone != request.Phone)
        {
            user.Phone = request.Phone;
            fields.Add("phone");
        }

        _databaseContext.Users.Update(user);
        await _databaseContext.SaveChangesAsync(cancellationToken);

        await _userAccountService.RemoveUserStateAsync(request.ContextUserId, cancellationToken);
        if (fields.Count > 0)
        {
            await _eventPublisher.PublishUserUpdatedAsync(user.Id, user.UserName, fields, cancellationToken);
        }

        return EmptyCommandResponse.Default;
    }
}
