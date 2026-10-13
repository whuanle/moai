using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MailKit;
using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;
using MoAI.AIPlugin.Attributes;
using MoAI.AIPlugin.Contracts;
using MoAI.AIPlugin.Dynamic.Models;
using MoAI.Infra.Exceptions;

namespace MoAI.AIPlugin.Dynamic.Plugins;

/// <summary>
/// SMTP 邮件发送（动态插件）：用实例配置里的 SMTP 服务器与账号发送邮件，支持多收件人/抄送/密送、HTML 正文与 Base64 附件.
/// </summary>
/// <remarks>
/// 协议栈为 MailKit（<see cref="SmtpClient"/>），连接、认证、发送均带超时护栏；
/// Message-ID 由插件预生成并回显，便于与收件侧（imap_email_query）联动核对。
/// </remarks>
[AiPlugin(key: "smtp_email_send", Name = "SMTP 邮件发送", Description = "通过 SMTP 协议连接邮箱发送邮件：支持多个收件人、抄送、密送、HTML 正文与 Base64 附件，发送成功返回 Message-ID")]
public class SmtpEmailSendPlugin : IDynamicPluginRuntime<SmtpEmailSendRequest, SmtpEmailSendResponse, SmtpEmailSendConfig>
{
    /// <summary>
    /// 单封邮件附件数量上限.
    /// </summary>
    private const int MaxAttachmentCount = 10;

    /// <summary>
    /// 单个附件大小上限（字节）.
    /// </summary>
    private const long MaxAttachmentItemBytes = 20 * 1024 * 1024;

    /// <summary>
    /// 主题最大长度.
    /// </summary>
    private const int MaxSubjectLength = 500;

    /// <summary>
    /// 正文最大长度.
    /// </summary>
    private const int MaxBodyLength = 100_000;

    private string _host = string.Empty;
    private int _port;
    private SecureSocketOptions _security = SecureSocketOptions.SslOnConnect;
    private string _username = string.Empty;
    private string _password = string.Empty;
    private string _fromAddress = string.Empty;
    private string _fromName = string.Empty;
    private int _timeoutSeconds;
    private long _maxAttachmentBytes;

    /// <inheritdoc/>
    public static string GetParamsExampleValue()
    {
        return """
            {
              "To": "boss@example.com;team@example.com", // 收件人，多个用逗号或分号分隔（必填）
              "Cc": "",                                  // 抄送（可选）
              "Bcc": "",                                 // 密送（可选）
              "Subject": "MoAI 日报：2026-10-12 销售汇总",  // 主题（必填）
              "Body": "<h3>今日 GMV</h3><p>合计 128.6 万元，环比 +9.2%</p>", // 正文（必填）
              "IsHtml": true,                            // 正文是否为 HTML（默认 false=纯文本）
              "ReplyTo": "",                             // 回复地址（可选）
              "Attachments": []                          // 附件，如 [{"FileName":"报表.xlsx","ContentBase64":"..."}]
            }
            """;
    }

    /// <inheritdoc/>
    public static string GetConfigExampleValue()
    {
        return """
            {
              "Host": "smtp.exmail.qq.com",     // SMTP 服务器地址（必填）
              "Port": 465,                      // 端口（默认 465）
              "Security": "ssl",                // 加密模式：ssl / starttls / none（默认 ssl）
              "Username": "bot@example.com",    // SMTP 登录账号（必填）
              "Password": "你的授权码",           // 登录密码或授权码（必填）
              "FromAddress": "",                // 发件人地址（可选，留空取 Username）
              "FromName": "MoAI 机器人",          // 发件人显示名（可选）
              "TimeoutSeconds": 15,             // 超时秒数（默认 15）
              "MaxAttachmentMb": 10             // 附件总大小上限 MB（默认 10）
            }
            """;
    }

    /// <inheritdoc/>
    public Task<string?> InitAsync(SmtpEmailSendConfig config)
    {
        if (string.IsNullOrWhiteSpace(config.Host))
        {
            return Task.FromResult<string?>("Host 不能为空，请填写 SMTP 服务器地址");
        }

        if (config.Port < 1 || config.Port > 65535)
        {
            return Task.FromResult<string?>("Port 必须在 1-65535 之间");
        }

        if (string.IsNullOrWhiteSpace(config.Username))
        {
            return Task.FromResult<string?>("Username 不能为空，请填写 SMTP 登录账号");
        }

        if (string.IsNullOrWhiteSpace(config.Password))
        {
            return Task.FromResult<string?>("Password 不能为空，请填写 SMTP 登录密码或授权码");
        }

        SecureSocketOptions security;
        try
        {
            security = MailSecurityParser.Parse(config.Security);
        }
        catch (BusinessException ex)
        {
            return Task.FromResult<string?>(ex.Message);
        }

        var fromAddress = (config.FromAddress ?? string.Empty).Trim();
        if (fromAddress.Length == 0)
        {
            fromAddress = config.Username.Trim();
        }

        if (!EmailRecipientParser.TryParseAddress(fromAddress, out _))
        {
            return Task.FromResult<string?>($"发件人地址不是合法邮箱：{fromAddress}，请在配置里填写 FromAddress");
        }

        _host = config.Host.Trim();
        _port = config.Port;
        _security = security;
        _username = config.Username.Trim();
        _password = config.Password;
        _fromAddress = fromAddress;
        _fromName = (config.FromName ?? string.Empty).Trim();
        _timeoutSeconds = Math.Clamp(config.TimeoutSeconds, 1, 120);
        _maxAttachmentBytes = Math.Clamp(config.MaxAttachmentMb, 1, 50) * 1024L * 1024L;
        return Task.FromResult<string?>(null);
    }

    /// <inheritdoc/>
    public async Task<SmtpEmailSendResponse> RunAsync(SmtpEmailSendRequest request, CancellationToken cancellationToken)
    {
        var to = EmailRecipientParser.Parse(request.To, "To");
        var cc = SplitOptional(request.Cc, "Cc");
        var bcc = SplitOptional(request.Bcc, "Bcc");
        var replyTo = EmailRecipientParser.ParseSingle(request.ReplyTo, "ReplyTo");

        var subject = (request.Subject ?? string.Empty).Trim();
        if (subject.Length == 0)
        {
            throw new BusinessException(400, "邮件主题 Subject 不能为空");
        }

        if (subject.Length > MaxSubjectLength)
        {
            throw new BusinessException(400, $"邮件主题 Subject 超长（{subject.Length} 字符），最长 {MaxSubjectLength}");
        }

        var body = request.Body ?? string.Empty;
        if (body.Length == 0)
        {
            throw new BusinessException(400, "邮件正文 Body 不能为空");
        }

        if (body.Length > MaxBodyLength)
        {
            throw new BusinessException(400, $"邮件正文 Body 超长（{body.Length} 字符），最长 {MaxBodyLength}");
        }

        var attachments = BuildAttachments(request.Attachments);

        var message = BuildMessage(subject, body, request.IsHtml, replyTo, to, cc, bcc, attachments);

        await SendAsync(message, cancellationToken).ConfigureAwait(false);

        return new SmtpEmailSendResponse
        {
            MessageId = message.MessageId ?? string.Empty,
            From = _fromAddress,
            To = to,
            Cc = cc,
            Bcc = bcc,
            Subject = subject,
            AttachmentCount = attachments.Count,
            AttachmentTotalBytes = attachments.Sum(x => (long)x.Content.Length),
        };
    }

    /// <summary>
    /// 拆分可选地址串；空输入返回空列表.
    /// </summary>
    private static IReadOnlyList<string> SplitOptional(string? raw, string fieldName)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return Array.Empty<string>();
        }

        return EmailRecipientParser.Parse(raw, fieldName);
    }

    /// <summary>
    /// 校验并解码附件：数量、单文件大小、总大小三重护栏，文件名剥离路径分隔符.
    /// </summary>
    private List<(string FileName, byte[] Content)> BuildAttachments(List<SmtpEmailAttachmentItem>? items)
    {
        var result = new List<(string, byte[])>();
        if (items == null || items.Count == 0)
        {
            return result;
        }

        if (items.Count > MaxAttachmentCount)
        {
            throw new BusinessException(400, $"附件数量超过上限：{items.Count} 个，最多 {MaxAttachmentCount} 个");
        }

        long total = 0;
        foreach (var item in items)
        {
            var fileName = SanitizeFileName(item?.FileName ?? string.Empty);
            if (fileName.Length == 0)
            {
                throw new BusinessException(400, "附件 FileName 不能为空");
            }

            var base64 = (item?.ContentBase64 ?? string.Empty).Trim();
            if (base64.Length == 0)
            {
                throw new BusinessException(400, $"附件 {fileName} 的 ContentBase64 不能为空");
            }

            byte[] content;
            try
            {
                content = Convert.FromBase64String(base64);
            }
            catch (FormatException)
            {
                throw new BusinessException(400, $"附件 {fileName} 的 ContentBase64 不是合法 Base64");
            }

            if (content.Length > MaxAttachmentItemBytes)
            {
                throw new BusinessException(400, $"附件 {fileName} 超过单文件上限 20MB");
            }

            total += content.Length;
            if (total > _maxAttachmentBytes)
            {
                throw new BusinessException(400, $"附件总大小超过上限 {_maxAttachmentBytes / 1024 / 1024}MB（配置 MaxAttachmentMb），实际 {total / 1024 / 1024}MB");
            }

            result.Add((fileName, content));
        }

        return result;
    }

    /// <summary>
    /// 清洗附件文件名：取最后一段路径并去掉非法字符.
    /// </summary>
    private static string SanitizeFileName(string raw)
    {
        var name = raw.Replace('\\', '/').Split('/').LastOrDefault() ?? string.Empty;
        return name.Trim();
    }

    /// <summary>
    /// 组装 MIME 消息.
    /// </summary>
    private MimeMessage BuildMessage(
        string subject,
        string body,
        bool isHtml,
        string replyTo,
        IReadOnlyList<string> to,
        IReadOnlyList<string> cc,
        IReadOnlyList<string> bcc,
        List<(string FileName, byte[] Content)> attachments)
    {
        var message = new MimeMessage
        {
            // 预生成 Message-ID：发送后可回显给调用方，也便于收件侧按 Message-ID 核对.
            MessageId = $"<{Guid.CreateVersion7():N}@{DomainOf(_fromAddress)}>",
        };

        message.From.Add(string.IsNullOrEmpty(_fromName) ? new MailboxAddress(string.Empty, _fromAddress) : new MailboxAddress(_fromName, _fromAddress));
        foreach (var addr in to)
        {
            message.To.Add(InternetAddress.Parse(addr));
        }

        foreach (var addr in cc)
        {
            message.Cc.Add(InternetAddress.Parse(addr));
        }

        foreach (var addr in bcc)
        {
            message.Bcc.Add(InternetAddress.Parse(addr));
        }

        if (replyTo.Length > 0)
        {
            message.ReplyTo.Add(InternetAddress.Parse(replyTo));
        }

        message.Subject = subject;

        var builder = new BodyBuilder();
        if (isHtml)
        {
            builder.HtmlBody = body;
        }
        else
        {
            builder.TextBody = body;
        }

        foreach (var (fileName, content) in attachments)
        {
            builder.Attachments.Add(fileName, content);
        }

        message.Body = builder.ToMessageBody();
        return message;
    }

    /// <summary>
    /// 建立连接、认证并发送；统一把 MailKit 异常翻译成可读业务异常.
    /// </summary>
    private async Task SendAsync(MimeMessage message, CancellationToken cancellationToken)
    {
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(TimeSpan.FromSeconds(_timeoutSeconds));

        using var client = new SmtpClient
        {
            Timeout = _timeoutSeconds * 1000,
        };
        try
        {
            await client.ConnectAsync(_host, _port, _security, timeoutCts.Token).ConfigureAwait(false);
            await client.AuthenticateAsync(_username, _password, timeoutCts.Token).ConfigureAwait(false);
            await client.SendAsync(message, timeoutCts.Token).ConfigureAwait(false);
            await client.DisconnectAsync(true, timeoutCts.Token).ConfigureAwait(false);
        }
        catch (AuthenticationException ex)
        {
            throw new BusinessException(401, $"SMTP 认证失败：账号或密码/授权码错误（{FirstLine(ex.Message)}）");
        }
        catch (SslHandshakeException ex)
        {
            throw new BusinessException(502, $"SMTP TLS 握手失败：请检查 Security 配置与端口是否匹配（{FirstLine(ex.Message)}）");
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new BusinessException(504, $"SMTP 连接或发送超时（TimeoutSeconds={_timeoutSeconds}），请检查服务器地址与端口");
        }
        catch (ServiceNotConnectedException ex)
        {
            throw new BusinessException(502, $"SMTP 连接未建立（{FirstLine(ex.Message)}）");
        }
        catch (Exception ex) when (ex is not BusinessException)
        {
            throw new BusinessException(502, $"SMTP 发送失败：{FirstLine(ex.Message)}");
        }
    }

    /// <summary>
    /// 从邮箱地址取域名部分（Message-ID 右侧），取不到时回退 localhost.
    /// </summary>
    private static string DomainOf(string address)
    {
        var index = address.LastIndexOf('@');
        return index >= 0 && index < address.Length - 1 ? address[(index + 1)..] : "localhost";
    }

    private static string FirstLine(string message)
    {
        var line = (message ?? string.Empty).Split('\n').FirstOrDefault()?.Trim('\r') ?? string.Empty;
        return string.IsNullOrWhiteSpace(line) ? "未知错误" : line;
    }
}
