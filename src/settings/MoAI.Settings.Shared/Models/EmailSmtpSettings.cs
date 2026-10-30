using System.Text.Json.Serialization;

namespace MoAI.Settings.Models;

/// <summary>
/// SMTP 加密方式.
/// </summary>
public enum EmailSmtpSecurity
{
    /// <summary>
    /// 隐式 TLS（通常 465 端口）.
    /// </summary>
    [JsonPropertyName("ssl")]
    Ssl = 0,

    /// <summary>
    /// 明文连接后升级 TLS（通常 587 端口）.
    /// </summary>
    [JsonPropertyName("starttls")]
    StartTls = 1,

    /// <summary>
    /// 不加密（通常 25 端口，仅建议内网中继使用）.
    /// </summary>
    [JsonPropertyName("none")]
    None = 2,
}

/// <summary>
/// SMTP 邮件服务配置（供业务模块读取，是否启用由超级管理员在系统设置页控制）.
/// </summary>
public class EmailSmtpSettings
{
    /// <summary>
    /// 默认端口（隐式 TLS）.
    /// </summary>
    public const int DefaultPort = 465;

    /// <summary>
    /// 是否启用 SMTP 邮件服务.
    /// </summary>
    public bool Enabled { get; init; }

    /// <summary>
    /// SMTP 服务器地址.
    /// </summary>
    public string Host { get; init; } = string.Empty;

    /// <summary>
    /// SMTP 服务器端口.
    /// </summary>
    public int Port { get; init; } = DefaultPort;

    /// <summary>
    /// 加密方式.
    /// </summary>
    public EmailSmtpSecurity Security { get; init; } = EmailSmtpSecurity.Ssl;

    /// <summary>
    /// SMTP 登录账号（空表示免认证，例如内网中继）.
    /// </summary>
    public string Username { get; init; } = string.Empty;

    /// <summary>
    /// SMTP 登录密码或授权码.
    /// </summary>
    public string Password { get; init; } = string.Empty;

    /// <summary>
    /// 发件人邮箱地址（配置留空时取登录账号）.
    /// </summary>
    public string FromAddress { get; init; } = string.Empty;

    /// <summary>
    /// 发件人显示名.
    /// </summary>
    public string FromName { get; init; } = string.Empty;
}
