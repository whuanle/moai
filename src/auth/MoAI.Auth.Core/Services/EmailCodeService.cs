using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MoAI.Auth.Models;
using MoAI.Database;
using MoAI.Database.Seed;
using MoAI.Infra;
using MoAI.Infra.Exceptions;
using MoAI.Notification.Models;
using MoAI.Notification.Services;
using MoAI.Settings.Models;
using MoAI.Settings.Services;
using StackExchange.Redis.Extensions.Core.Abstractions;

namespace MoAI.Auth.Services;

/// <summary>
/// 邮箱验证码服务实现：验证码 6 位数字，Redis 存储 10 分钟有效，同邮箱同用途 60 秒发送冷却，连续错 5 次作废；
/// 邮件模板优先取系统设置绑定的通知中心模板，未绑定时按用途回退内置模板（注册/登录/找回密码，HTML），再回退代码内置纯文本模板，
/// 变量支持 {code} {siteName} {expireMinutes}.
/// </summary>
public class EmailCodeService : IEmailCodeService
{
    private const int CodeTtlMinutes = 10;

    private const int ResendCooldownSeconds = 60;

    private const int MaxVerifyAttempts = 5;

    private readonly DatabaseContext _databaseContext;
    private readonly IRedisDatabase _redisDatabase;
    private readonly IEmailSettingsService _emailSettingsService;
    private readonly IEmailSenderService _emailSenderService;
    private readonly INotificationTemplateRenderService _templateRenderService;
    private readonly ILogger<EmailCodeService> _logger;
    private readonly SystemOptions _systemOptions;

    private sealed record EmailCodeState(string Code, int Attempts);

    /// <summary>
    /// Initializes a new instance of the <see cref="EmailCodeService"/> class.
    /// </summary>
    public EmailCodeService(
        DatabaseContext databaseContext,
        IRedisDatabase redisDatabase,
        IEmailSettingsService emailSettingsService,
        IEmailSenderService emailSenderService,
        INotificationTemplateRenderService templateRenderService,
        ILogger<EmailCodeService> logger,
        SystemOptions systemOptions)
    {
        _databaseContext = databaseContext;
        _redisDatabase = redisDatabase;
        _emailSettingsService = emailSettingsService;
        _emailSenderService = emailSenderService;
        _templateRenderService = templateRenderService;
        _logger = logger;
        _systemOptions = systemOptions;
    }

    /// <inheritdoc/>
    public async Task<bool> IsRegisterCodeRequiredAsync(CancellationToken cancellationToken)
    {
        var smtp = await _emailSettingsService.GetAsync(cancellationToken);
        if (!smtp.Enabled)
        {
            return false;
        }

        return await GetSettingBoolAsync(SettingDefinitions.RegisterEmailCodeEnabledKey, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<bool> IsPasswordResetEnabledAsync(CancellationToken cancellationToken)
    {
        var smtp = await _emailSettingsService.GetAsync(cancellationToken);
        return smtp.Enabled;
    }

    /// <inheritdoc/>
    public async Task SendCodeAsync(EmailCodePurpose purpose, string email, CancellationToken cancellationToken)
    {
        email = NormalizeEmail(email);
        var smtp = await _emailSettingsService.GetAsync(cancellationToken);

        if (purpose == EmailCodePurpose.Register)
        {
            if (!smtp.Enabled || !await GetSettingBoolAsync(SettingDefinitions.RegisterEmailCodeEnabledKey, cancellationToken))
            {
                throw new BusinessException("注册验证码未开启，请直接注册.") { StatusCode = 400 };
            }

            if (await _databaseContext.Users.AnyAsync(u => u.Email == email, cancellationToken))
            {
                throw new BusinessException("邮箱 {0} 已被注册", email) { StatusCode = 409 };
            }
        }
        else if (purpose == EmailCodePurpose.Login)
        {
            // 登录验证码：公共发送端点已拒绝该用途，只有持有一次性登录票据的内部端点才会走到这里
            if (!smtp.Enabled)
            {
                throw new BusinessException("SMTP 邮件服务未启用，无法发送登录验证码.") { StatusCode = 400 };
            }

            if (!await _databaseContext.Users.AnyAsync(u => u.Email == email && u.IsDeleted == 0, cancellationToken))
            {
                throw new BusinessException("该邮箱未注册.") { StatusCode = 404 };
            }
        }
        else
        {
            if (!smtp.Enabled)
            {
                throw new BusinessException("SMTP 邮件服务未启用，无法通过邮箱找回密码，请联系管理员重置.") { StatusCode = 400 };
            }

            if (!await _databaseContext.Users.AnyAsync(u => u.Email == email && u.IsDeleted == 0, cancellationToken))
            {
                throw new BusinessException("该邮箱未注册.") { StatusCode = 404 };
            }
        }

        var cooldownKey = CooldownKey(purpose, email);
        if (await _redisDatabase.Database.KeyExistsAsync(cooldownKey))
        {
            throw new BusinessException("验证码发送过于频繁，请稍后再试.") { StatusCode = 429 };
        }

        var code = RandomNumberGenerator.GetInt32(0, 1000000).ToString("D6", CultureInfo.InvariantCulture);
        var siteName = await ResolveSiteNameAsync(cancellationToken);
        var values = new Dictionary<string, string>
        {
            ["code"] = code,
            ["siteName"] = siteName,
            ["expireMinutes"] = CodeTtlMinutes.ToString(CultureInfo.InvariantCulture)
        };

        var (subject, body, isHtml) = await RenderTemplateAsync(purpose, values, cancellationToken);
        await _emailSenderService.SendAsync(new EmailMessage
        {
            To = new[] { email },
            Subject = subject,
            Body = body,
            IsHtml = isHtml
        }, cancellationToken);

        // 发送成功后才落验证码与冷却标记：发送失败可立即重试，不占用冷却
        await _redisDatabase.Database.StringSetAsync(
            CodeKey(purpose, email),
            JsonSerializer.Serialize(new EmailCodeState(code, 0)),
            TimeSpan.FromMinutes(CodeTtlMinutes));
        await _redisDatabase.Database.StringSetAsync(
            cooldownKey,
            "1",
            TimeSpan.FromSeconds(ResendCooldownSeconds));
    }

    /// <inheritdoc/>
    public async Task VerifyAndConsumeAsync(EmailCodePurpose purpose, string email, string code, CancellationToken cancellationToken)
    {
        email = NormalizeEmail(email);
        code = code.Trim();

        var key = CodeKey(purpose, email);
        var json = await _redisDatabase.Database.StringGetAsync(key);
        if (json.IsNull || json.IsNullOrEmpty)
        {
            throw new BusinessException("验证码已过期或未发送，请重新获取.") { StatusCode = 400 };
        }

        var state = JsonSerializer.Deserialize<EmailCodeState>(json.ToString())!;
        if (!string.Equals(state.Code, code, StringComparison.Ordinal))
        {
            var attempts = state.Attempts + 1;
            if (attempts >= MaxVerifyAttempts)
            {
                await _redisDatabase.Database.KeyDeleteAsync(key);
                throw new BusinessException("验证码错误次数过多，请重新获取.") { StatusCode = 400 };
            }

            var ttl = await _redisDatabase.Database.KeyTimeToLiveAsync(key);
            await _redisDatabase.Database.StringSetAsync(
                key,
                JsonSerializer.Serialize(state with { Attempts = attempts }),
                ttl ?? TimeSpan.FromMinutes(CodeTtlMinutes));
            throw new BusinessException("验证码错误，请重新输入.") { StatusCode = 400 };
        }

        await _redisDatabase.Database.KeyDeleteAsync(key);
    }

    private static string NormalizeEmail(string email) => email.Trim().ToLowerInvariant();

    private static string CodeKey(EmailCodePurpose purpose, string email) => $"emailcode:{purpose.ToString().ToLowerInvariant()}:{email}";

    private static string CooldownKey(EmailCodePurpose purpose, string email) => $"emailcode:cd:{purpose.ToString().ToLowerInvariant()}:{email}";

    private async Task<bool> GetSettingBoolAsync(string key, CancellationToken cancellationToken)
    {
        var value = await _databaseContext.Settings
            .Where(s => s.Key == key)
            .Select(s => s.Value)
            .FirstOrDefaultAsync(cancellationToken);

        if (string.IsNullOrWhiteSpace(value))
        {
            value = SettingDefinitions.Find(key)?.DefaultValue;
        }

        return string.Equals(value, "true", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// 读取验证码邮件绑定的通知中心模板 id 与兜底模板，绑定 id 为空时回退内置模板.
    /// </summary>
    /// <returns>返回（绑定的模板 id、兜底主题模板、兜底正文模板、内置模板 code）.</returns>
    private async Task<(long? BoundTemplateId, string DefaultSubject, string DefaultBody, string? BuiltinCode)> LoadTemplatesAsync(EmailCodePurpose purpose, CancellationToken cancellationToken)
    {
        var (key, defaultSubject, defaultBody, builtinCode) = purpose switch
        {
            EmailCodePurpose.Register => (
                SettingDefinitions.SmtpRegisterCodeTemplateIdKey,
                SettingDefinitions.DefaultRegisterCodeSubject,
                SettingDefinitions.DefaultRegisterCodeBody,
                (string?)NotificationTemplateSeed.RegisterEmailCode),
            EmailCodePurpose.Login => (
                SettingDefinitions.SmtpLoginCodeTemplateIdKey,
                SettingDefinitions.DefaultLoginCodeSubject,
                SettingDefinitions.DefaultLoginCodeBody,
                (string?)NotificationTemplateSeed.LoginEmailCode),
            _ => (
                SettingDefinitions.SmtpResetCodeTemplateIdKey,
                SettingDefinitions.DefaultResetCodeSubject,
                SettingDefinitions.DefaultResetCodeBody,
                (string?)NotificationTemplateSeed.ResetEmailCode)
        };

        var boundValue = await _databaseContext.Settings
            .Where(s => s.Key == key)
            .Select(s => s.Value)
            .FirstOrDefaultAsync(cancellationToken);

        long? boundTemplateId = null;
        if (long.TryParse(boundValue, out var parsedId) && parsedId > 0)
        {
            boundTemplateId = parsedId;
        }

        return (boundTemplateId, defaultSubject, defaultBody, builtinCode);
    }

    /// <summary>
    /// 渲染验证码邮件模板：优先绑定的通知中心模板，未绑定（或绑定模板缺失/渲染失败，如管理员误删、变量不匹配）时
    /// 回退内置模板，再回退代码内置纯文本模板，保证验证码链路永不因模板配置中断.
    /// </summary>
    /// <returns>返回（主题、正文、正文是否 HTML）.</returns>
    private async Task<(string Subject, string Body, bool IsHtml)> RenderTemplateAsync(
        EmailCodePurpose purpose,
        IReadOnlyDictionary<string, string> values,
        CancellationToken cancellationToken)
    {
        var (boundTemplateId, defaultSubject, defaultBody, builtinCode) = await LoadTemplatesAsync(purpose, cancellationToken);

        if (boundTemplateId != null)
        {
            try
            {
                var rendered = await _templateRenderService.RenderByIdAsync(boundTemplateId.Value, values, cancellationToken);
                return (rendered.Title, rendered.Content, rendered.ContentType == Database.Enums.NotificationContentType.Html);
            }
            catch (BusinessException ex)
            {
                _logger.LogWarning(ex, "绑定的验证码邮件模板渲染失败（模板 id：{TemplateId}），回退默认模板", boundTemplateId.Value);
            }
        }
        else if (builtinCode != null)
        {
            try
            {
                var rendered = await _templateRenderService.RenderByBuiltinCodeAsync(builtinCode, values, cancellationToken);
                if (rendered != null)
                {
                    return (rendered.Title, rendered.Content, rendered.ContentType == Database.Enums.NotificationContentType.Html);
                }
            }
            catch (BusinessException ex)
            {
                _logger.LogWarning(ex, "内置验证码邮件模板（{BuiltinCode}）渲染失败，回退默认模板", builtinCode);
            }
        }

        return (
            EmailCodeTemplate.Render(defaultSubject, values),
            EmailCodeTemplate.Render(defaultBody, values),
            false);
    }

    /// <summary>
    /// 网站名称取自系统设置，为空回退配置文件默认名称（模板 {siteName} 变量）.
    /// </summary>
    private async Task<string> ResolveSiteNameAsync(CancellationToken cancellationToken)
    {
        var name = (await _databaseContext.Settings
            .Where(s => s.Key == SettingDefinitions.SystemNameKey)
            .Select(s => s.Value)
            .FirstOrDefaultAsync(cancellationToken) ?? string.Empty).Trim();

        return name.Length > 0 ? name : _systemOptions.Name;
    }
}
