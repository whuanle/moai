using MoAI.Infra.Models;
using System.Text.Json.Serialization;

namespace MoAI.AI.Models;

/// <summary>
/// 插件调用.
/// </summary>
public class DefaultAiProcessingPluginCall : AiProcessingPluginCall
{
    /// <summary>
    /// id.
    /// </summary>
    public string ToolCallId { get; set; } = string.Empty;

    /// <summary>
    /// 插件类型.
    /// </summary>
    public PluginType PluginType { get; set; }

    /// <summary>
    /// 插件 key.
    /// </summary>
    public string PluginKey { get; set; } = string.Empty;

    /// <summary>
    /// 插件名称.
    /// </summary>
    public string PluginName { get; set; } = string.Empty;

    /// <summary>
    /// 被调用的函数名称.
    /// </summary>
    public string FunctionName { get; set; } = string.Empty;

    /// <summary>
    /// 执行插件的参数.
    /// </summary>
    public IReadOnlyCollection<KeyValueString> Params { get; set; } = Array.Empty<KeyValueString>();

    /// <summary>
    /// 执行插件的结果.
    /// </summary>
    public string Result { get; set; } = string.Empty;
}
