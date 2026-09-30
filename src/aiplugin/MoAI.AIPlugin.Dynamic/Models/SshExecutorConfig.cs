using System.ComponentModel;

namespace MoAI.AIPlugin.Dynamic.Models;

/// <summary>
/// SSH 命令执行插件配置（每个实例独立保存）.
/// </summary>
public class SshExecutorConfig
{
    /// <summary>
    /// SSH 主机.
    /// </summary>
    [Description("SSH 主机，例如 10.0.0.5 或 server.internal")]
    public string Host { get; set; } = string.Empty;

    /// <summary>
    /// SSH 端口.
    /// </summary>
    [Description("SSH 端口，1-65535（默认 22）")]
    public int Port { get; set; } = 22;

    /// <summary>
    /// 登录用户名.
    /// </summary>
    [Description("登录用户名；建议使用权限受控的运维账号")]
    public string Username { get; set; } = string.Empty;

    /// <summary>
    /// 密码（与私钥二选一）.
    /// </summary>
    [Description("密码（与私钥二选一）")]
    public string Password { get; set; } = string.Empty;

    /// <summary>
    /// 私钥（PEM/OpenSSH 格式，与密码二选一）.
    /// </summary>
    [Description("私钥全文（PEM/OpenSSH 格式，与密码二选一；已填时优先于密码）")]
    public string PrivateKey { get; set; } = string.Empty;

    /// <summary>
    /// 命令白名单（必填，逗号分隔的命令前缀）.
    /// </summary>
    [Description("命令白名单（必填，逗号分隔的命令前缀，如 systemctl status,journalctl,df,free,tail,uptime）；命令（含管道的每一段）必须命中任一前缀才放行，为空则拒绝一切命令")]
    public string CommandWhitelist { get; set; } = string.Empty;

    /// <summary>
    /// 连接与命令超时秒数（1-300）.
    /// </summary>
    [Description("连接与命令超时秒数，取值 1-300（默认 30）")]
    public int CommandTimeoutSeconds { get; set; } = 30;

    /// <summary>
    /// 输出最大字符数（256-65536）.
    /// </summary>
    [Description("标准输出/错误输出各自的最大返回字符数，取值 256-65536（默认 8192），超出截断")]
    public int MaxOutputChars { get; set; } = 8192;
}
