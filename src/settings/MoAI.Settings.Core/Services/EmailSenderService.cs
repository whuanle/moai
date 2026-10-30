using System.Net.Mail;
using MailKit;
using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;
using MoAI.Infra.Exceptions;
using MoAI.Settings.Models;
using MoAI.Settings.Services;

namespace MoAI.Settings.Services;

/// <summary>
/// 平台通知邮件发送服务：读取系统设置中的 SMTP 配置，经 MailKit 发送；连接、认证、发送统一 15 秒超时护栏.
/// </summary>
public class EmailSenderService : IEmailSenderService
{
    private const int TimeoutSeconds = 15;

    private const int MaxSubjectLength = 500;

    private readonly IEmailSettingsService _emailSettingsService;

    /// <summary>
    /// Initializes a new instance of the <see cref="EmailSenderService"/> class.
    /// </summary>
    /// <param name="emailSettingsService">SMTP 配置读取服务.</param>
    public EmailSenderService(IEmailSettingsService emailSettingsService)
    {
        _emailSettingsService = emailSettingsService;
    }

    /// <inheritdoc/>
    public async Task<EmailSendResult> SendAsync(EmailMessage message, CancellationToken cancellationToken)
    {
        var settings = await _emailSettingsService.GetAsync(cancellationToken);
        if (!settings.Enabled || settings.Host.Length == 0)
        {
            throw new BusinessException("SMTP 邮件服务未启用或未配置服务器地址") { StatusCode = 400 };
        }

        var fromAddress = settings.FromAddress;
        if (fromAddress.Length == 0 || !MailAddress.TryCreate(fromAddress, out _))
        {
            throw new BusinessException("SMTP 邮件服务未配置合法的发件人地址") { StatusCode = 400 };
        }

        if (message.To.Count == 0)
        {
            throw new BusinessException("收件人地址不能为空") { StatusCode = 400 };
        }

        var subject = message.Subject.Trim();
        if (subject.Length == 0)
        {
            throw new BusinessException("邮件主题不能为空") { StatusCode = 400 };
        }

        if (subject.Length > MaxSubjectLength)
        {
            throw new BusinessException($"邮件主题超长，最长 {MaxSubjectLength} 个字符") { StatusCode = 400 };
        }

        if (message.Body.Length == 0)
        {
            throw new BusinessException("邮件正文不能为空") { StatusCode = 400 };
        }

        var mimeMessage = BuildMessage(settings, subject, message);
        await SendAsync(settings, mimeMessage, cancellationToken).ConfigureAwait(false);

        return new EmailSendResult
        {
            MessageId = mimeMessage.MessageId ?? string.Empty,
            From = fromAddress
        };
    }

    /// <summary>
    /// 组装 MIME 消息：预生成 Message-ID 便于与收件侧核对.
    /// </summary>
    private static MimeMessage BuildMessage(EmailSmtpSettings settings, string subject, EmailMessage message)
    {
        var domain = "localhost";
        var atIndex = settings.FromAddress.LastIndexOf('@');
        if (atIndex >= 0 && atIndex < settings.FromAddress.Length - 1)
        {
            domain = settings.FromAddress[(atIndex + 1)..];
        }

        var mimeMessage = new MimeMessage
        {
            MessageId = $"<{Guid.CreateVersion7():N}@{domain}>"
        };

        mimeMessage.From.Add(
            settings.FromName.Length == 0
                ? new MailboxAddress(string.Empty, settings.FromAddress)
                : new MailboxAddress(settings.FromName, settings.FromAddress));

        foreach (var to in message.To)
        {
            if (!MailAddress.TryCreate(to, out _))
            {
                throw new BusinessException($"收件人地址不是合法的邮箱地址：{to}") { StatusCode = 400 };
            }

            mimeMessage.To.Add(InternetAddress.Parse(to.Trim()));
        }

        mimeMessage.Subject = subject;
        var builder = new BodyBuilder();
        if (message.IsHtml)
        {
            builder.HtmlBody = message.Body;
        }
        else
        {
            builder.TextBody = message.Body;
        }

        mimeMessage.Body = builder.ToMessageBody();
        return mimeMessage;
    }

    /// <summary>
    /// 建立连接、按需认证并发送；统一把 MailKit 异常翻译成可读业务异常.
    /// </summary>
    private static async Task SendAsync(EmailSmtpSettings settings, MimeMessage message, CancellationToken cancellationToken)
    {
        var security = settings.Security switch
        {
            EmailSmtpSecurity.StartTls => SecureSocketOptions.StartTls,
            EmailSmtpSecurity.None => SecureSocketOptions.None,
            _ => SecureSocketOptions.SslOnConnect
        };

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(TimeSpan.FromSeconds(TimeoutSeconds));

        using var client = new MailKit.Net.Smtp.SmtpClient
        {
            Timeout = TimeoutSeconds * 1000
        };

        try
        {
            await client.ConnectAsync(settings.Host, settings.Port, security, timeoutCts.Token).ConfigureAwait(false);
            if (settings.Username.Length > 0)
            {
                await client.AuthenticateAsync(settings.Username, settings.Password, timeoutCts.Token).ConfigureAwait(false);
            }

            await client.SendAsync(message, timeoutCts.Token).ConfigureAwait(false);
            await client.DisconnectAsync(true, timeoutCts.Token).ConfigureAwait(false);
        }
        catch (AuthenticationException ex)
        {
            throw new BusinessException($"SMTP 认证失败：账号或密码/授权码错误（{FirstLine(ex.Message)}）") { StatusCode = 502 };
        }
        catch (SslHandshakeException ex)
        {
            throw new BusinessException($"SMTP TLS 握手失败：请检查加密方式与端口是否匹配（{FirstLine(ex.Message)}）") { StatusCode = 502 };
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new BusinessException($"SMTP 连接或发送超时（{TimeoutSeconds} 秒），请检查服务器地址与端口") { StatusCode = 504 };
        }
        catch (ServiceNotConnectedException ex)
        {
            throw new BusinessException($"SMTP 连接未建立（{FirstLine(ex.Message)}）") { StatusCode = 502 };
        }
        catch (Exception ex) when (ex is not BusinessException)
        {
            throw new BusinessException($"SMTP 发送失败：{FirstLine(ex.Message)}") { StatusCode = 502 };
        }
    }

    private static string FirstLine(string message)
    {
        var line = (message ?? string.Empty).Split('\n').FirstOrDefault()?.Trim('\r') ?? string.Empty;
        return string.IsNullOrWhiteSpace(line) ? "未知错误" : line;
    }
}
