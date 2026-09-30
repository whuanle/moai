using System.ComponentModel;

namespace MoAI.AIPlugin.Dynamic.Models;

/// <summary>
/// SSH 命令执行插件响应.
/// </summary>
public class SshExecutorResponse
{
    /// <summary>
    /// 实际下发的命令.
    /// </summary>
    public string Command { get; set; } = string.Empty;

    /// <summary>
    /// 退出码（连接失败/超时时为 null）.
    /// </summary>
    public int? ExitStatus { get; set; }

    /// <summary>
    /// 是否成功（退出码为 0）.
    /// </summary>
    public bool Ok { get; set; }

    /// <summary>
    /// 标准输出（按 MaxOutputChars 截断）.
    /// </summary>
    public string Output { get; set; } = string.Empty;

    /// <summary>
    /// 标准错误输出（按 MaxOutputChars 截断）.
    /// </summary>
    public string ErrorOutput { get; set; } = string.Empty;

    /// <summary>
    /// 执行耗时（毫秒）.
    /// </summary>
    public long DurationMs { get; set; }
}
