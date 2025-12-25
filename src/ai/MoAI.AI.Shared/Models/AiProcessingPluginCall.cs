using MoAI.Infra.Models;
using MoAI.Wiki.Models;
using System.Text.Json.Serialization;

namespace MoAI.AI.Models;

/// <summary>
/// 插件调用.
/// </summary>
public interface AiProcessingPluginCall
{
    /// <summary>
    /// 插件类型.
    /// </summary>
    public PluginType PluginType { get; }

    /// <summary>
    /// 插件 key.
    /// </summary>
    public string PluginKey { get; }

    /// <summary>
    /// 插件名称.
    /// </summary>
    public string PluginName { get; }

    /// <summary>
    /// 执行插件的参数.
    /// </summary>
    public IReadOnlyCollection<KeyValueString> Params { get; }

    /// <summary>
    /// 执行插件的结果.
    /// </summary>
    public string Result { get; }
}
