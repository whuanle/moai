using MediatR;
using MoAI.Auth.Commands;
using MoAI.Auth.Models;
using MoAI.Auth.Services;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Models;

namespace MoAI.Auth.Handlers;

/// <summary>
/// <inheritdoc cref="SendEmailCodeCommand"/>.
/// </summary>
public class SendEmailCodeCommandHandler : IRequestHandler<SendEmailCodeCommand, EmptyCommandResponse>
{
    private readonly IEmailCodeService _emailCodeService;

    /// <summary>
    /// Initializes a new instance of the <see cref="SendEmailCodeCommandHandler"/> class.
    /// </summary>
    /// <param name="emailCodeService">邮箱验证码服务.</param>
    public SendEmailCodeCommandHandler(IEmailCodeService emailCodeService)
    {
        _emailCodeService = emailCodeService;
    }

    /// <inheritdoc/>
    public async Task<EmptyCommandResponse> Handle(SendEmailCodeCommand request, CancellationToken cancellationToken)
    {
        // 登录验证码只允许在密码校验通过后凭一次性登录票据发送，公共匿名端点拒绝，防止向任意邮箱轰炸验证码
        if (request.Purpose == EmailCodePurpose.Login)
        {
            throw new BusinessException("验证码用途无效.") { StatusCode = 400 };
        }

        await _emailCodeService.SendCodeAsync(request.Purpose, request.Email, cancellationToken);

        return EmptyCommandResponse.Default;
    }
}
