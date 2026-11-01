using Microsoft.EntityFrameworkCore;
using MoAI.Auth.Models;
using MoAI.Database;
using MoAI.Database.Seed;
using MoAI.Settings.Services;

namespace MoAI.Auth.Services;

/// <summary>
/// 登录安全策略服务实现：从 setting 表读取策略，未配置回退 <see cref="SettingDefinitions"/> 默认值；
/// 邮箱验证码策略生效要求 SMTP 邮件服务已启用（双开关语义）.
/// </summary>
public class LoginSecurityService : ILoginSecurityService
{
    private readonly DatabaseContext _databaseContext;
    private readonly IEmailSettingsService _emailSettingsService;

    /// <summary>
    /// Initializes a new instance of the <see cref="LoginSecurityService"/> class.
    /// </summary>
    public LoginSecurityService(DatabaseContext databaseContext, IEmailSettingsService emailSettingsService)
    {
        _databaseContext = databaseContext;
        _emailSettingsService = emailSettingsService;
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

        return mode == LoginEmailCodeMode.Always || loginFailCount >= 1;
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
