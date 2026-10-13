using System.ComponentModel;

namespace MoAI.AIPlugin.Dynamic.Models;

/// <summary>
/// IMAP 邮箱读取插件配置（每个实例独立保存）.
/// </summary>
public class ImapEmailQueryConfig
{
    /// <summary>
    /// IMAP 服务器地址.
    /// </summary>
    [Description("IMAP 服务器地址，例如 imap.exmail.qq.com 或 127.0.0.1")]
    public string Host { get; set; } = string.Empty;

    /// <summary>
    /// IMAP 端口.
    /// </summary>
    [Description("IMAP 端口，1-65535（默认 993；明文调试常用 143）")]
    public int Port { get; set; } = 993;

    /// <summary>
    /// 加密模式.
    /// </summary>
    [Description("加密模式：ssl（默认，993 端口）/ starttls（143 端口）/ none（明文，仅内网调试）")]
    public string Security { get; set; } = "ssl";

    /// <summary>
    /// IMAP 账号.
    /// </summary>
    [Description("IMAP 登录账号，通常是邮箱地址")]
    public string Username { get; set; } = string.Empty;

    /// <summary>
    /// IMAP 密码或授权码.
    /// </summary>
    [Description("IMAP 登录密码；网易/QQ/企业邮箱等通常要求使用「授权码」而非网页登录密码")]
    public string Password { get; set; } = string.Empty;

    /// <summary>
    /// 默认文件夹.
    /// </summary>
    [Description("默认读取的邮箱文件夹（默认 INBOX 收件箱）；请求里可临时指定 Folder")]
    public string Folder { get; set; } = "INBOX";

    /// <summary>
    /// 连接与命令超时秒数.
    /// </summary>
    [Description("连接与命令超时秒数，1-120（默认 15）")]
    public int TimeoutSeconds { get; set; } = 15;

    /// <summary>
    /// 正文最大字符数.
    /// </summary>
    [Description("detail 模式返回正文的最大字符数，0-20000（默认 4000，0 表示不截断）")]
    public int MaxBodyChars { get; set; } = 4000;
}
