using MediatR;

namespace MoAI.AIPlugin.Commands;

/// <summary>
/// 按插件名称累加一次插件使用次数（Redis 缓冲，Hangfire 定时落库到 plugins.counter）.
/// </summary>
public class IncrementPluginUsageCommand : IRequest
{
    /// <summary>
    /// 插件名称（plugins.plugin_name，运行入口传入的 key）.
    /// </summary>
    public string Key { get; init; } = string.Empty;

    /// <summary>
    /// 团队 id；大于 0 时仅匹配该团队自有插件，否则优先匹配系统插件（TeamId=0）.
    /// </summary>
    public int TeamId { get; init; }
}
