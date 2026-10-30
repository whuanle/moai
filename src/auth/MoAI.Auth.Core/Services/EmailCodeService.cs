using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using MoAI.Auth.Models;
using MoAI.Database;
using MoAI.Database.Seed;
using MoAI.Infra;
using MoAI.Infra.Exceptions;
using MoAI.Settings.Models;
using MoAI.Settings.Services;
using StackExchange.Redis.Extensions.Core.Abstractions;

namespace MoAI.Auth.Services;

/// <summary>
/// 邮箱验证码服务实现：验证码 6 位数字，Redis 存储 10 分钟有效，同邮箱同用途 60 秒发送冷却，连续错 5 次作废；
/// 邮件主题/正文使用系统设置中的模板（SmartFormat 插值，支持 {code} {siteName} {expireMinutes}），留空回退内置默认模板.
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
        SystemOptions systemOptions)
    {
        _databaseContext = databaseContext;
        _redisDatabase = redisDatabase;
        _emailSettingsService = emailSettingsService;
        _emailSenderService = emailSenderService;
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

        var (subjectTemplate, bodyTemplate) = await LoadTemplatesAsync(purpose, cancellationToken);
        await _emailSenderService.SendAsync(new EmailMessage
        {
            To = new[] { email },
            Subject = EmailCodeTemplate.Render(subjectTemplate, values),
            Body = EmailCodeTemplate.Render(bodyTemplate, values)
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
    /// 读取验证码邮件主题/正文模板，配置为空时回退内置默认模板.
    /// </summary>
    private async Task<(string Subject, string Body)> LoadTemplatesAsync(EmailCodePurpose purpose, CancellationToken cancellationToken)
    {
        var keys = purpose == EmailCodePurpose.Register
            ? new[] { SettingDefinitions.SmtpRegisterCodeSubjectKey, SettingDefinitions.SmtpRegisterCodeBodyKey }
            : new[] { SettingDefinitions.SmtpResetCodeSubjectKey, SettingDefinitions.SmtpResetCodeBodyKey };

        var values = await _databaseContext.Settings
            .Where(s => keys.Contains(s.Key))
            .ToDictionaryAsync(s => s.Key, s => s.Value, cancellationToken);

        var subject = purpose == EmailCodePurpose.Register
            ? SettingDefinitions.DefaultRegisterCodeSubject
            : SettingDefinitions.DefaultResetCodeSubject;
        var body = purpose == EmailCodePurpose.Register
            ? SettingDefinitions.DefaultRegisterCodeBody
            : SettingDefinitions.DefaultResetCodeBody;

        if (values.TryGetValue(keys[0], out var subjectValue) && !string.IsNullOrWhiteSpace(subjectValue))
        {
            subject = subjectValue;
        }

        if (values.TryGetValue(keys[1], out var bodyValue) && !string.IsNullOrWhiteSpace(bodyValue))
        {
            body = bodyValue;
        }

        return (subject, body);
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
