using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.Account.Commands;
using MoAI.Account.Services;
using MoAI.Database;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Models;

namespace MoAI.Account.Handlers;

/// <summary>
/// <inheritdoc cref="UpdateUserAvatarCommand"/>
/// </summary>
public class UpdateUserAvatarCommandHandler : IRequestHandler<UpdateUserAvatarCommand, EmptyCommandResponse>
{
    private readonly DatabaseContext _databaseContext;
    private readonly IUserAccountService _userAccountService;

    /// <summary>
    /// Initializes a new instance of the <see cref="UpdateUserAvatarCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext"></param>
    /// <param name="userAccountService"></param>
    public UpdateUserAvatarCommandHandler(DatabaseContext databaseContext, IUserAccountService userAccountService)
    {
        _databaseContext = databaseContext;
        _userAccountService = userAccountService;
    }

    /// <inheritdoc/>
    public async Task<EmptyCommandResponse> Handle(UpdateUserAvatarCommand request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.ObjectKey))
        {
            throw new BusinessException("头像文件不能为空") { StatusCode = 400 };
        }

        var user = await _databaseContext.Users.FirstOrDefaultAsync(x => x.Id == request.ContextUserId, cancellationToken);
        if (user == null)
        {
            throw new BusinessException("用户不存在") { StatusCode = 404 };
        }

        user.AvatarPath = request.ObjectKey;

        _databaseContext.Users.Update(user);
        await _databaseContext.SaveChangesAsync(cancellationToken);

        await _userAccountService.RemoveUserStateAsync(request.ContextUserId, cancellationToken);

        return EmptyCommandResponse.Default;
    }
}
