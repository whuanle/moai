using MoAI.Infra.Models;

namespace MoAI.App.AIAssistant.Models;

/// <summary>
/// 对话参数.
/// </summary>
public class AIAssistantChatObject
{
    /// <summary>
    /// 话题名称.
    /// </summary>
    public string Title { get; init; } = string.Empty;

    /// <summary>
    /// 提示词，第一次对话时带上，如果后续不需要修改则不需要再次传递.
    /// </summary>
    public string Prompt { get; init; } = string.Empty;

    /// <summary>
    /// 要使用的 AI 模型.
    /// </summary>
    public int ModelId { get; init; }

    /// <summary>
    /// 要使用的知识库，如果用户不在知识库用户内，则必须是公开的.
    /// </summary>
    public int WikiId { get; init; }

    /// <summary>
    /// 要使用的插件 id 列表，用户必须有权使用这些插件.
    /// </summary>
    public IReadOnlyCollection<int> PluginIds { get; init; } = Array.Empty<int>();

    /// <summary>
    /// 配置，字典适配不同的 AI 模型.
    /// </summary>
    public IReadOnlyCollection<KeyValueString> ExecutionSettings { get; init; } = Array.Empty<KeyValueString>();
}
