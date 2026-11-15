using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.Account.Commands;
using MoAI.Account.Services;
using MoAI.Database;
using MoAI.Infra.Events;
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
    private readonly IAccessEventPublisher _eventPublisher;

    /// <summary>
    /// Initializes a new instance of the <see cref="UpdateUserAvatarCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext"></param>
    /// <param name="userAccountService"></param>
    /// <param name="eventPublisher">接入事件发布器.</param>
    public UpdateUserAvatarCommandHandler(DatabaseContext databaseContext, IUserAccountService userAccountService, IAccessEventPublisher eventPublisher)
    {
        _databaseContext = databaseContext;
        _userAccountService = userAccountService;
        _eventPublisher = eventPublisher;
    }

    /// <inheritdoc/>
    public async Task<EmptyCommandResponse> Handle(UpdateUserAvatarCommand request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.ObjectKey))
        {
            throw new BusinessException("头像文件不能为空") { StatusCode = 400 };
        }

        // 仅允许引用已完成上传并登记的文件，防止任意伪造 objectKey
        var fileExists = await _databaseContext.Files
            .AnyAsync(f => f.ObjectKey == request.ObjectKey && f.IsUploaded && f.IsDeleted == 0, cancellationToken);
        if (!fileExists)
        {
            throw new BusinessException("头像文件不存在或未完成上传.") { StatusCode = 404 };
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
        await _eventPublisher.PublishUserUpdatedAsync(user.Id, user.UserName, ["avatar"], cancellationToken);

        return EmptyCommandResponse.Default;
    }
}
