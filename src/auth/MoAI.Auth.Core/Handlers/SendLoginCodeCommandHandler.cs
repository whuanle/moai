using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.Auth.Commands;
using MoAI.Auth.Models;
using MoAI.Auth.Services;
using MoAI.Database;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Models;

namespace MoAI.Auth.Handlers;

/// <summary>
/// <inheritdoc cref="SendLoginCodeCommand"/>.
/// </summary>
public class SendLoginCodeCommandHandler : IRequestHandler<SendLoginCodeCommand, EmptyCommandResponse>
{
    private readonly ILoginChallengeService _challengeService;
    private readonly IEmailCodeService _emailCodeService;
    private readonly DatabaseContext _databaseContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="SendLoginCodeCommandHandler"/> class.
    /// </summary>
    public SendLoginCodeCommandHandler(ILoginChallengeService challengeService, IEmailCodeService emailCodeService, DatabaseContext databaseContext)
    {
        _challengeService = challengeService;
        _emailCodeService = emailCodeService;
        _databaseContext = databaseContext;
    }

    /// <inheritdoc/>
    public async Task<EmptyCommandResponse> Handle(SendLoginCodeCommand request, CancellationToken cancellationToken)
    {
        // 票据即凭证：只有密码校验通过后的登录会话才能触发登录验证码发送
        var payload = await _challengeService.PeekTicketAsync(request.LoginTicket, cancellationToken);
        if (payload == null || payload.Factor != LoginChallengeFactors.EmailCode)
        {
            throw new BusinessException("登录会话已过期，请重新登录.") { StatusCode = 401 };
        }

        var email = await _databaseContext.Users
            .Where(u => u.Id == payload.UserId && u.IsDeleted == 0)
            .Select(u => u.Email)
            .FirstOrDefaultAsync(cancellationToken);
        if (string.IsNullOrEmpty(email))
        {
            throw new BusinessException("用户不存在") { StatusCode = 401 };
        }

        // 60 秒发送冷却由 EmailCodeService 内部保证
        await _emailCodeService.SendCodeAsync(EmailCodePurpose.Login, email, cancellationToken);

        return EmptyCommandResponse.Default;
    }
}
