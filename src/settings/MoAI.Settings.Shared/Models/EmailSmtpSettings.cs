namespace MoAI.Settings.Models;

/// <summary>
/// 平台邮箱验证配置（供业务模块读取）：SMTP 连接配置统一在通知中心渠道维护，此处仅暴露总开关与绑定渠道是否可用.
/// </summary>
public class EmailSmtpSettings
{
    /// <summary>
    /// 是否可用（总开关开启且绑定的 SMTP 渠道存在并启用）.
    /// </summary>
    public bool Enabled { get; init; }

    /// <summary>
    /// 绑定的通知中心 SMTP 渠道 id（未绑定为 0）.
    /// </summary>
    public long ChannelId { get; init; }
}
