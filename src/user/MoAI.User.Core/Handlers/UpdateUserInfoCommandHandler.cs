using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.Database;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Models;
using MoAI.Login.Commands;
using MoAI.User.Commands;

namespace MoAI.User.Handlers;

/// <summary>
/// <inheritdoc cref="UpdateUserInfoCommand"/>
/// </summary>
public class UpdateUserInfoCommandHandler : IRequestHandler<UpdateUserInfoCommand, EmptyCommandResponse>
{
    private readonly DatabaseContext _databaseContext;
    private readonly IMediator _mediator;

    /// <summary>
    /// Initializes a new instance of the <see cref="UpdateUserInfoCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext"></param>
    /// <param name="mediator"></param>
    public UpdateUserInfoCommandHandler(DatabaseContext databaseContext, IMediator mediator)
    {
        _databaseContext = databaseContext;
        _mediator = mediator;
    }

    /// <inheritdoc/>
    public async Task<EmptyCommandResponse> Handle(UpdateUserInfoCommand request, CancellationToken cancellationToken)
    {
        var user = await _databaseContext.Users.FirstOrDefaultAsync(x => x.Id == request.UserId);

        if (user == null)
        {
            throw new BusinessException("用户不存在") { StatusCode = 404 };
        }

        if (user.IsDisable)
        {
            throw new BusinessException("用户已被禁用") { StatusCode = 403 };
        }

        var existUser = await _databaseContext.Users.Where(u => u.Id != request.UserId).Where(u =>
                                  u.UserName == request.UserName || u.Email == request.Email)
                              .FirstOrDefaultAsync(cancellationToken);

        if (existUser != null)
        {
            throw new BusinessException("已存在相同用户名或邮箱") { StatusCode = 409 };
        }

        user.UserName = request.UserName;
        user.Email = request.Email;
        user.NickName = request.NickName;
        user.Phone = request.Phone;
        user.AvatarPath = request.AvatarPath;

        _databaseContext.Users.Update(user);
        await _databaseContext.SaveChangesAsync(cancellationToken);

        await _mediator.Send(new RefreshUserStateCommand
        {
            UserId = user.Id,
        });

        return EmptyCommandResponse.Default;
    }
}
