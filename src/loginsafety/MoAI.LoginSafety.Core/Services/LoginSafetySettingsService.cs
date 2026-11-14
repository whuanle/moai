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
        SettingDefinitions.LoginSafetyNotifyChannelIdsKey,
        SettingDefinitions.LoginSafetyNotifyEmailsKey,
        SettingDefinitions.LoginSafetyIpFailThresholdKey,
        SettingDefinitions.LoginSafetyIpFailWindowMinutesKey,
        SettingDefinitions.LoginSafetyIpBanMinutesKey,
        SettingDefinitions.LoginSafetyDistinctUserThresholdKey,
        SettingDefinitions.LoginSafetyNewIpDaysKey,
        SettingDefinitions.LoginSafetyTravelWindowMinutesKey
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

        // 渠道 ID 列表：保存侧已校验为正整数，读取侧仅做宽容解析（非法段忽略）
        var notifyChannelIds = Resolve(SettingDefinitions.LoginSafetyNotifyChannelIdsKey)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(part => long.TryParse(part, out var channelId) ? channelId : 0)
            .Where(id => id > 0)
            .Distinct()
            .ToArray();

        // 告警收件人邮箱列表：逗号分隔，读取侧宽容解析（非法段忽略，保存侧校验格式）
        var notifyEmails = Resolve(SettingDefinitions.LoginSafetyNotifyEmailsKey)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(email => System.Net.Mail.MailAddress.TryCreate(email, out _))
            .Distinct()
            .ToArray();

        return new LoginSafetySettings
        {
            Enabled = string.Equals(Resolve(SettingDefinitions.LoginSafetyEnabledKey), "true", StringComparison.OrdinalIgnoreCase),
            NotifyChannelIds = notifyChannelIds,
            NotifyEmails = notifyEmails,
            IpFailThreshold = Math.Clamp(ResolveInt(SettingDefinitions.LoginSafetyIpFailThresholdKey, 20), 1, 10000),
            IpFailWindowMinutes = Math.Clamp(ResolveInt(SettingDefinitions.LoginSafetyIpFailWindowMinutesKey, 10), 1, 525600),
            IpBanMinutes = Math.Clamp(ResolveInt(SettingDefinitions.LoginSafetyIpBanMinutesKey, 30), 1, 525600),
            DistinctUserThreshold = Math.Clamp(ResolveInt(SettingDefinitions.LoginSafetyDistinctUserThresholdKey, 5), 1, 10000),
            NewIpDays = Math.Clamp(ResolveInt(SettingDefinitions.LoginSafetyNewIpDaysKey, 90), 0, 3650),
            TravelWindowMinutes = Math.Clamp(ResolveInt(SettingDefinitions.LoginSafetyTravelWindowMinutesKey, 120), 1, 525600)
        };
    }
}
