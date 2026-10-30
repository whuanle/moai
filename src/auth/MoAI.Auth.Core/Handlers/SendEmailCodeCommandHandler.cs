using MediatR;
using MoAI.Auth.Commands;
using MoAI.Auth.Services;
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
        await _emailCodeService.SendCodeAsync(request.Purpose, request.Email, cancellationToken);

        return EmptyCommandResponse.Default;
    }
}
