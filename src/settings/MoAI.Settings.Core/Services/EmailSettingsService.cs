using Microsoft.EntityFrameworkCore;
using MoAI.Database;
using MoAI.Database.Enums;
using MoAI.Database.Seed;
using MoAI.Settings.Models;
using MoAI.Settings.Services;

namespace MoAI.Settings.Services;

/// <summary>
/// 邮箱验证配置读取服务：SMTP 连接配置统一在通知中心渠道维护，这里只解析总开关与绑定渠道是否可用.
/// </summary>
public class EmailSettingsService : IEmailSettingsService
{
    private static readonly string[] Keys =
    {
        SettingDefinitions.SmtpEnabledKey,
        SettingDefinitions.SmtpChannelIdKey
    };

    private readonly DatabaseContext _databaseContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="EmailSettingsService"/> class.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    public EmailSettingsService(DatabaseContext databaseContext)
    {
        _databaseContext = databaseContext;
    }

    /// <inheritdoc/>
    public async Task<EmailSmtpSettings> GetAsync(CancellationToken cancellationToken)
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

        var enabled = string.Equals(Resolve(SettingDefinitions.SmtpEnabledKey), "true", StringComparison.OrdinalIgnoreCase);
        if (!enabled || !long.TryParse(Resolve(SettingDefinitions.SmtpChannelIdKey), out var channelId) || channelId <= 0)
        {
            return new EmailSmtpSettings { Enabled = false };
        }

        // 绑定的渠道必须存在且启用（软删除由全局过滤器剔除）
        var channelEnabled = await _databaseContext.NotificationChannels
            .Where(c => c.Id == channelId && c.Type == (int)NotificationChannelType.Smtp)
            .Select(c => (bool?)c.Enabled)
            .FirstOrDefaultAsync(cancellationToken);

        if (channelEnabled != true)
        {
            return new EmailSmtpSettings { Enabled = false };
        }

        return new EmailSmtpSettings { Enabled = true, ChannelId = channelId };
    }
}
