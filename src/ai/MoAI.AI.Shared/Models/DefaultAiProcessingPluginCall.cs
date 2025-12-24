using MoAI.Infra.Models;

namespace MoAI.AI.Models;

/// <summary>
/// 插件调用.
/// </summary>
public class DefaultAiProcessingPluginCall : AiProcessingPluginCall
{
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
    /// 信息，如果报错，会有错误信息.
    /// </summary>
    public string? Message { get; set; }

    /// <summary>
    /// 执行插件的参数.
    /// </summary>
    public IReadOnlyCollection<KeyValueString> Params { get; set; } = Array.Empty<KeyValueString>();

    /// <summary>
    /// 执行插件的结果.
    /// </summary>
    public string Result { get; set; } = string.Empty;
}
