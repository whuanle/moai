using MoAI.Wiki.Models;
using System.Text.Json.Serialization;

namespace MoAI.AI.Models;

/// <summary>
/// 执行信息.
/// </summary>
public class DefaultAiProcessingChoice
{
    /// <summary>
    /// 是否已经推送返回.
    /// </summary>
    public bool IsPush { get; set; }

    /// <summary>
    /// 流类型.
    /// </summary>
    public AiProcessingChatStreamType StreamType { get; set; }

    /// <summary>
    /// 流状态.
    /// </summary>
    public AiProcessingChatStreamState StreamState { get; set; }

    /// <summary>
    /// 插件调用.
    /// </summary>
    public DefaultAiProcessingPluginCall? PluginCall { get; set; }

    /// <summary>
    /// 文本执行结果.
    /// </summary>
    public DefaultAiProcessingTextCall? TextCall { get; set; }

    /// <summary>
    /// 转换 AiProcessingChoice.
    /// </summary>
    /// <returns></returns>
    public AiProcessingChoice ToAiProcessingChoice()
    {
        return new AiProcessingChoice
        {
            StreamType = StreamType,
            StreamState = StreamState,
            PluginCall = PluginCall,
            TextCall = TextCall
        };
    }
}
