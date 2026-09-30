using System.ComponentModel;

namespace MoAI.AIPlugin.Dynamic.Models;

/// <summary>
/// SSH 命令执行插件请求参数.
/// </summary>
public class SshExecutorRequest
{
    /// <summary>
    /// 待执行命令.
    /// </summary>
    [Description("待执行命令（单条；管道 | 可用但每一段都必须命中白名单；; && || & ` $( 换行 一律拒绝），例如 journalctl -u nginx --since \"1 hour ago\" | tail -n 100")]
    public string Command { get; set; } = string.Empty;
}
