using Microsoft.EntityFrameworkCore;
using MoAI.Database;
using MoAI.Database.Seed;

namespace MoAI.LoginSafety.Services;

/// <summary>
/// 登录安全风控设置读取服务实现：从 setting 表读取 LS_* 键，未配置回退 <see cref="SettingDefinitions"/> 默认值.
/// </summary>
public class LoginSafetySettingsService : ILoginSafetySettingsService
{
    private static readonly string[] Keys =
    {
        SettingDefinitions.LoginSafetyEnabledKey,
        SettingDefinitions.LoginSafetyNotifyUserEnabledKey,
        SettingDefinitions.LoginSafetyNotifyAdminEnabledKey,
        SettingDefinitions.LoginSafetyAdminRecipientsKey,
        SettingDefinitions.LoginSafetyIpFailThresholdKey,
        SettingDefinitions.LoginSafetyIpFailWindowMinutesKey,
        SettingDefinitions.LoginSafetyIpBanMinutesKey,
        SettingDefinitions.LoginSafetyDistinctUserThresholdKey,
        SettingDefinitions.LoginSafetyNewIpDaysKey,
        SettingDefinitions.LoginSafetyTravelWindowMinutesKey,
        SettingDefinitions.LoginSafetyUserAlertSubjectKey,
        SettingDefinitions.LoginSafetyUserAlertBodyKey,
        SettingDefinitions.LoginSafetyAdminAlertSubjectKey,
        SettingDefinitions.LoginSafetyAdminAlertBodyKey
    };

    private readonly DatabaseContext _databaseContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="LoginSafetySettingsService"/> class.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    public LoginSafetySettingsService(DatabaseContext databaseContext)
    {
        _databaseContext = databaseContext;
    }

    /// <inheritdoc/>
    public async Task<LoginSafetySettings> GetAsync(CancellationToken cancellationToken)
    {
        var values = await _databaseContext.Settings
            .Where(s => Keys.Contains(s.Key))
            .ToDictionaryAsync(s => s.Key, s => s.Value, cancellationToken);

        string Resolve(string key)
        {
            if (values.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value))
            {
                return value;
            }

            return SettingDefinitions.Find(key)?.DefaultValue ?? string.Empty;
        }

        int ResolveInt(string key, int fallback)
        {
            return int.TryParse(Resolve(key), out var value) && value >= 0 ? value : fallback;
        }

        var recipients = Resolve(SettingDefinitions.LoginSafetyAdminRecipientsKey)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        return new LoginSafetySettings
        {
            Enabled = string.Equals(Resolve(SettingDefinitions.LoginSafetyEnabledKey), "true", StringComparison.OrdinalIgnoreCase),
            NotifyUserEnabled = string.Equals(Resolve(SettingDefinitions.LoginSafetyNotifyUserEnabledKey), "true", StringComparison.OrdinalIgnoreCase),
            NotifyAdminEnabled = string.Equals(Resolve(SettingDefinitions.LoginSafetyNotifyAdminEnabledKey), "true", StringComparison.OrdinalIgnoreCase),
            AdminRecipients = recipients,
            IpFailThreshold = Math.Clamp(ResolveInt(SettingDefinitions.LoginSafetyIpFailThresholdKey, 20), 1, 10000),
            IpFailWindowMinutes = Math.Clamp(ResolveInt(SettingDefinitions.LoginSafetyIpFailWindowMinutesKey, 10), 1, 525600),
            IpBanMinutes = Math.Clamp(ResolveInt(SettingDefinitions.LoginSafetyIpBanMinutesKey, 30), 1, 525600),
            DistinctUserThreshold = Math.Clamp(ResolveInt(SettingDefinitions.LoginSafetyDistinctUserThresholdKey, 5), 1, 10000),
            NewIpDays = Math.Clamp(ResolveInt(SettingDefinitions.LoginSafetyNewIpDaysKey, 90), 0, 3650),
            TravelWindowMinutes = Math.Clamp(ResolveInt(SettingDefinitions.LoginSafetyTravelWindowMinutesKey, 120), 1, 525600),
            UserAlertSubject = Resolve(SettingDefinitions.LoginSafetyUserAlertSubjectKey),
            UserAlertBody = Resolve(SettingDefinitions.LoginSafetyUserAlertBodyKey),
            AdminAlertSubject = Resolve(SettingDefinitions.LoginSafetyAdminAlertSubjectKey),
            AdminAlertBody = Resolve(SettingDefinitions.LoginSafetyAdminAlertBodyKey)
        };
    }
}
