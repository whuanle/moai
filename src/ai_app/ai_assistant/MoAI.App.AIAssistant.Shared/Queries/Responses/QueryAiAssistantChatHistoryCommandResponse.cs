using MoAI.AI.Models;
using MoAI.App.AIAssistant.Models;
using MoAI.Infra.Models;

namespace MoAI.App.AIAssistant.Queries.Responses;

/// <summary>
/// 对话记录结果.
/// </summary>
public class QueryAiAssistantChatHistoryCommandResponse
{
    /// <summary>
    /// id.
    /// </summary>
    public Guid ChatId { get; init; }

    /// <summary>
    /// 头像图标.
    /// </summary>
    public string Avatar { get; init; } = string.Empty;

    /// <summary>
    /// 话题名称.
    /// </summary>
    public string Title { get; init; } = string.Empty;

    /// <summary>
    /// 提示词，第一次对话时带上，如果后续不需要修改则不需要再次传递.
    /// </summary>
    public string? Prompt { get; init; } = string.Empty;

    /// <summary>
    /// 要使用的 AI 模型.
    /// </summary>
    public int ModelId { get; init; }

    /// <summary>
    /// 要使用的知识库列表，可使用已加入的或公开的知识库.
    /// </summary>
    public IReadOnlyCollection<int> WikiIds { get; init; } = Array.Empty<int>();

    /// <summary>
    /// 要使用的插件列表，填插件的 Key，Tool 类插件的 Key 就是其对应的模板 Key.
    /// </summary>
    public IReadOnlyCollection<string> Plugins { get; init; } = Array.Empty<string>();

    /// <summary>
    /// 配置，字典适配不同的 AI 模型.
    /// </summary>
    public IReadOnlyCollection<KeyValueString> ExecutionSettings { get; init; } = Array.Empty<KeyValueString>();

    /// <summary>
    /// 消耗的 token 统计.
    /// </summary>
    public OpenAIChatCompletionsUsage TokenUsage { get; init; } = default!;

    /// <summary>
    /// 创建时间.
    /// </summary>
    public DateTimeOffset CreateTime { get; init; }

    /// <summary>
    /// 最后更新时间.
    /// </summary>
    public DateTimeOffset UpdateTime { get; init; }

    /// <summary>
    /// 历史对话或者上下文信息.
    /// </summary>
    public virtual IReadOnlyCollection<ChatContentItem> ChatHistory { get; init; } = Array.Empty<ChatContentItem>();
}