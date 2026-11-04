using Microsoft.EntityFrameworkCore;
using MoAI.Auth.Models;
using MoAI.Database;
using MoAI.Database.Seed;
using MoAI.LoginSafety.Services;
using MoAI.Settings.Services;
using StackExchange.Redis.Extensions.Core.Abstractions;

namespace MoAI.Auth.Services;

/// <summary>
/// 登录安全策略服务实现：从 setting 表读取策略，未配置回退 <see cref="SettingDefinitions"/> 默认值；
/// 邮箱验证码策略生效要求 SMTP 邮件服务已启用（双开关语义）；图形验证码策略为分级递进的第一道防线.
/// </summary>
public class LoginSecurityService : ILoginSecurityService
{
    private readonly DatabaseContext _databaseContext;
    private readonly IEmailSettingsService _emailSettingsService;
    private readonly IRedisDatabase _redisDatabase;
    private readonly ILoginRiskService _loginRiskService;
    private readonly IPublicAuthRateLimiter _rateLimiter;

    /// <summary>
    /// Initializes a new instance of the <see cref="LoginSecurityService"/> class.
    /// </summary>
    public LoginSecurityService(
        DatabaseContext databaseContext,
        IEmailSettingsService emailSettingsService,
        IRedisDatabase redisDatabase,
        ILoginRiskService loginRiskService,
        IPublicAuthRateLimiter rateLimiter)
    {
        _databaseContext = databaseContext;
        _emailSettingsService = emailSettingsService;
        _redisDatabase = redisDatabase;
        _loginRiskService = loginRiskService;
        _rateLimiter = rateLimiter;
    }

    /// <inheritdoc/>
    public async Task<bool> IsForced2faAsync(CancellationToken cancellationToken)
    {
        var value = await GetSettingAsync(SettingDefinitions.LoginForced2faKey, cancellationToken);
        return string.Equals(value, "true", StringComparison.OrdinalIgnoreCase);
    }

    /// <inheritdoc/>
    public async Task<LoginEmailCodeMode> GetEmailCodeModeAsync(CancellationToken cancellationToken)
    {
        var value = (await GetSettingAsync(SettingDefinitions.LoginEmailCodeModeKey, cancellationToken))?.Trim().ToLowerInvariant();
        return value switch
        {
            "always" => LoginEmailCodeMode.Always,
            "after_fail" => LoginEmailCodeMode.AfterFail,
            _ => LoginEmailCodeMode.Off
        };
    }

    /// <inheritdoc/>
    public async Task<bool> ShouldRequireLoginCodeAsync(int loginFailCount, CancellationToken cancellationToken)
    {
        var mode = await GetEmailCodeModeAsync(cancellationToken);
        if (mode == LoginEmailCodeMode.Off)
        {
            return false;
        }

        var smtp = await _emailSettingsService.GetAsync(cancellationToken);
        if (!smtp.Enabled)
        {
            return false;
        }

        return mode == LoginEmailCodeMode.Always || loginFailCount >= await GetEmailCodeFailThresholdAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<LoginCaptchaMode> GetCaptchaModeAsync(CancellationToken cancellationToken)
    {
        var value = (await GetSettingAsync(SettingDefinitions.LoginCaptchaModeKey, cancellationToken))?.Trim().ToLowerInvariant();
        return value switch
        {
            "always" => LoginCaptchaMode.Always,
            "after_fail" => LoginCaptchaMode.AfterFail,
            _ => LoginCaptchaMode.Off
        };
    }

    /// <inheritdoc/>
    public async Task<bool> ShouldRequireCaptchaAsync(string purpose, string userName, string ip, CancellationToken cancellationToken)
    {
        var mode = await GetCaptchaModeAsync(cancellationToken);
        if (mode == LoginCaptchaMode.Off)
        {
            return false;
        }

        if (mode == LoginCaptchaMode.Always)
        {
            return true;
        }

        // after_fail：登录看用户名/IP 失败计数（任一命中），注册/找回看该 IP 本目的发码计数（首免费、再发需过码）
        if (purpose == LoginCaptchaPurposes.Login)
        {
            if (!string.IsNullOrWhiteSpace(userName))
            {
                var failCount = await _redisDatabase.GetAsync<int>($"login:fail:{userName.Trim()}");
                if (failCount >= 1)
                {
                    return true;
                }
            }

            return await _loginRiskService.GetIpFailCountAsync(ip, cancellationToken) >= 1;
        }

        if (string.IsNullOrWhiteSpace(ip))
        {
            return false;
        }

        return await _rateLimiter.GetCountAsync($"sendct_{purpose}", ip, cancellationToken) >= 1;
    }

    /// <inheritdoc/>
    public async Task<int> GetEmailCodeFailThresholdAsync(CancellationToken cancellationToken)
    {
        var value = await GetSettingAsync(SettingDefinitions.LoginEmailCodeFailThresholdKey, cancellationToken);
        return int.TryParse(value, out var threshold) && threshold >= 1 ? Math.Min(threshold, 10) : 3;
    }

    private async Task<string?> GetSettingAsync(string key, CancellationToken cancellationToken)
    {
        var value = await _databaseContext.Settings
            .Where(s => s.Key == key)
            .Select(s => s.Value)
            .FirstOrDefaultAsync(cancellationToken);

        if (string.IsNullOrWhiteSpace(value))
        {
            value = SettingDefinitions.Find(key)?.DefaultValue;
        }

        return value;
    }
}
