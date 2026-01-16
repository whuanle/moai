#pragma warning disable SKEXP0001 // 类型仅用于评估，在将来的更新中可能会被更改或删除。取消此诊断以继续。
#pragma warning disable SKEXP0040 // 类型仅用于评估，在将来的更新中可能会被更改或删除。取消此诊断以继续。
#pragma warning disable CA1849 // 当在异步方法中时，调用异步方法
#pragma warning disable CA1031 // 不捕获常规异常类型

using MoAI.Infra.Models;

namespace MoAI.AI.Chat.Models;

/// <summary>
/// 流式对话抽象.
/// </summary>
public class ProcessingChatStreamParameters : IUserIdContext
{
    /// <inheritdoc/>
    public int ContextUserId { get; init; }

    /// <inheritdoc/>
    public UserType ContextUserType { get; init; }

    /// <summary>
    /// 自定义标识符，用于标识本次对话请求.
    /// </summary>
    public string CompletionId { get; init; } = string.Empty;

    /// <summary>
    /// 对话 id.
    /// </summary>
    public Guid ChatId { get; init; }

    /// <summary>
    /// 用户问题.
    /// </summary>
    public string Question { get; init; } = string.Empty;

    /// <summary>
    /// 提示词.
    /// </summary>
    public string? Prompt { get; init; } = string.Empty;

    /// <summary>
    /// 图片 key，需要先调用接口上传图片.
    /// </summary>
    public string? FileKey { get; init; }

    /// <summary>
    /// 已有对话历史.
    /// </summary>
    public IReadOnlyCollection<RoleProcessingChoice> ChatMessages { get; init; } = Array.Empty<RoleProcessingChoice>();

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
}
