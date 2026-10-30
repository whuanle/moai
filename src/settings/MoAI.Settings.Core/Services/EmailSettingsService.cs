using Microsoft.EntityFrameworkCore;
using MoAI.Database;
using MoAI.Database.Seed;
using MoAI.Settings.Models;
using MoAI.Settings.Services;

namespace MoAI.Settings.Services;

/// <summary>
/// SMTP 邮件服务配置读取服务.
/// </summary>
public class EmailSettingsService : IEmailSettingsService
{
    private static readonly string[] Keys =
    {
        SettingDefinitions.SmtpEnabledKey,
        SettingDefinitions.SmtpHostKey,
        SettingDefinitions.SmtpPortKey,
        SettingDefinitions.SmtpSecurityKey,
        SettingDefinitions.SmtpUsernameKey,
        SettingDefinitions.SmtpPasswordKey,
        SettingDefinitions.SmtpFromAddressKey,
        SettingDefinitions.SmtpFromNameKey
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
        var host = Resolve(SettingDefinitions.SmtpHostKey);
        if (!enabled || host.Length == 0)
        {
            return new EmailSmtpSettings { Enabled = false };
        }

        if (!int.TryParse(Resolve(SettingDefinitions.SmtpPortKey), out var port) || port < 1 || port > 65535)
        {
            port = EmailSmtpSettings.DefaultPort;
        }

        var security = Resolve(SettingDefinitions.SmtpSecurityKey).Trim().ToLowerInvariant() switch
        {
            "starttls" => EmailSmtpSecurity.StartTls,
            "none" => EmailSmtpSecurity.None,
            _ => EmailSmtpSecurity.Ssl
        };

        var username = Resolve(SettingDefinitions.SmtpUsernameKey);
        var fromAddress = Resolve(SettingDefinitions.SmtpFromAddressKey);
        if (fromAddress.Length == 0)
        {
            fromAddress = username;
        }

        return new EmailSmtpSettings
        {
            Enabled = true,
            Host = host,
            Port = port,
            Security = security,
            Username = username,
            Password = Resolve(SettingDefinitions.SmtpPasswordKey),
            FromAddress = fromAddress,
            FromName = Resolve(SettingDefinitions.SmtpFromNameKey)
        };
    }
}
