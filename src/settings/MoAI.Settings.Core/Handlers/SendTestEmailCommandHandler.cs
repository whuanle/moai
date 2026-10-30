using MediatR;
using MoAI.Infra.Models;
using MoAI.Settings.Commands;
using MoAI.Settings.Models;
using MoAI.Settings.Services;

namespace MoAI.Settings.Handlers;

/// <summary>
/// <inheritdoc cref="SendTestEmailCommand"/>
/// </summary>
public class SendTestEmailCommandHandler : IRequestHandler<SendTestEmailCommand, EmptyCommandResponse>
{
    private readonly IEmailSenderService _emailSenderService;

    /// <summary>
    /// Initializes a new instance of the <see cref="SendTestEmailCommandHandler"/> class.
    /// </summary>
    /// <param name="emailSenderService">通知邮件发送服务.</param>
    public SendTestEmailCommandHandler(IEmailSenderService emailSenderService)
    {
        _emailSenderService = emailSenderService;
    }

    /// <inheritdoc/>
    public async Task<EmptyCommandResponse> Handle(SendTestEmailCommand request, CancellationToken cancellationToken)
    {
        var to = request.To.Trim();
        var now = DateTimeOffset.Now;
        await _emailSenderService.SendAsync(new EmailMessage
        {
            To = new[] { to },
            Subject = "MoAI 邮件服务测试",
            Body = $"""
                这是一封来自 MoAI 的测试邮件。

                收到本邮件说明 SMTP 邮件服务配置正确，平台通知邮件将使用该服务发送。

                发送时间：{now:yyyy-MM-dd HH:mm:ss zzz}
                """
        }, cancellationToken);

        return EmptyCommandResponse.Default;
    }
}
