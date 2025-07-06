using FastEndpoints;
using MoAI.User.Shared.Commands;
using MediatR;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Models;
using MoAI.User.Commands;

namespace MoAI.User.Endpoints;

/// <summary>
/// 上传头像.
/// </summary>
[HttpPost($"{ApiPrefix.Prefix}/upload_avatar")]
public class UploadtUserAvatarEndpoint : Endpoint<UploadtUserAvatarCommand, EmptyCommandResponse>
{
    private readonly IMediator _mediator;
    private readonly UserContext _userContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="UploadtUserAvatarEndpoint"/> class.
    /// </summary>
    /// <param name="mediator"></param>
    /// <param name="userContext"></param>
    public UploadtUserAvatarEndpoint(IMediator mediator, UserContext userContext)
    {
        _mediator = mediator;
        _userContext = userContext;
    }

    /// <inheritdoc/>
    public override async Task<EmptyCommandResponse> ExecuteAsync(UploadtUserAvatarCommand req, CancellationToken ct)
    {
        if (req.UserId != _userContext.UserId)
        {
            throw new BusinessException("没有操作权限") { StatusCode = 403 };
        }

        return await _mediator.Send(req, ct);
    }
}
