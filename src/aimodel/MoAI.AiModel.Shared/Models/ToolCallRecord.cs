using MoAI.Plugin.Models;

namespace MoAI.AiModel.Models;

/// <summary>
/// 工具调用记录.
/// </summary>
public class ToolCallRecord
{
    /// <summary>
    /// 工具类型.
    /// </summary>
    public PluginType PluginType { get; init; }

    /// <summary>
    /// 工具 id.
    /// </summary>
    public int ToolId { get; init; }

    /// <summary>
    /// 映射函数名称.
    /// </summary>
    public required string Key { get; init; }
}