using System.ComponentModel;

namespace MoAI.AIPlugin.Dynamic.Models;

/// <summary>
/// SMTP 邮件发送插件配置（每个实例独立保存）.
/// </summary>
public class SmtpEmailSendConfig
{
    /// <summary>
    /// SMTP 服务器地址.
    /// </summary>
    [Description("SMTP 服务器地址，例如 smtp.exmail.qq.com 或 127.0.0.1")]
    public string Host { get; set; } = string.Empty;

    /// <summary>
    /// SMTP 端口.
    /// </summary>
    [Description("SMTP 端口，1-65535（默认 465；明文调试常用 25，STARTTLS 常用 587）")]
    public int Port { get; set; } = 465;

    /// <summary>
    /// 加密模式.
    /// </summary>
    [Description("加密模式：ssl（默认，465 端口）/ starttls（587 端口）/ none（明文，仅内网调试）")]
    public string Security { get; set; } = "ssl";

    /// <summary>
    /// SMTP 账号.
    /// </summary>
    [Description("SMTP 登录账号，通常是邮箱地址；若 FromAddress 留空则同时作为发件人地址")]
    public string Username { get; set; } = string.Empty;

    /// <summary>
    /// SMTP 密码或授权码.
    /// </summary>
    [Description("SMTP 登录密码；网易/QQ/企业邮箱等通常要求使用「授权码」而非网页登录密码")]
    public string Password { get; set; } = string.Empty;

    /// <summary>
    /// 发件人地址.
    /// </summary>
    [Description("发件人地址（可选）；留空时取 Username。部分服务商要求必须与登录账号一致")]
    public string FromAddress { get; set; } = string.Empty;

    /// <summary>
    /// 发件人显示名.
    /// </summary>
    [Description("发件人显示名（可选），如「运营报表机器人」")]
    public string FromName { get; set; } = string.Empty;

    /// <summary>
    /// 连接与发送超时秒数.
    /// </summary>
    [Description("连接与发送超时秒数，1-120（默认 15）")]
    public int TimeoutSeconds { get; set; } = 15;

    /// <summary>
    /// 附件总大小上限（MB）.
    /// </summary>
    [Description("单次发送附件总大小上限（MB），1-50（默认 10）；超出直接拒绝")]
    public int MaxAttachmentMb { get; set; } = 10;
}
