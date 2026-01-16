using MoAI.AI.Models;
using MoAI.Infra.Models;

namespace MoAI.App.Apps.CommonApp.Responses;

/// <summary>
/// 对话历史记录响应.
/// </summary>
public class QueryAppChatHistoryCommandResponse
{
    /// <summary>
    /// 对话 id.
    /// </summary>
    public Guid ChatId { get; init; }

    /// <summary>
    /// 对话标题.
    /// </summary>
    public string Title { get; init; } = string.Empty;

    /// <summary>
    /// 头像.
    /// </summary>
    public string Avatar { get; init; } = string.Empty;

    /// <summary>
    /// 创建时间.
    /// </summary>
    public DateTimeOffset CreateTime { get; init; }

    /// <summary>
    /// 更新时间.
    /// </summary>
    public DateTimeOffset UpdateTime { get; init; }

    /// <summary>
    /// 对话历史记录.
    /// </summary>
    public IReadOnlyCollection<AppChatHistoryItem> ChatHistory { get; init; } = Array.Empty<AppChatHistoryItem>();
}

/// <summary>
/// 对话历史记录项.
/// </summary>
public class AppChatHistoryItem
{
    /// <summary>
    /// 记录 id.
    /// </summary>
    public long RecordId { get; init; }

    /// <summary>
    /// 角色名称: system、assistant、user、tool.
    /// </summary>
    public string AuthorName { get; init; } = string.Empty;

    /// <summary>
    /// 对话内容.
    /// </summary>
    public IReadOnlyCollection<AiProcessingChoice> Choices { get; init; } = Array.Empty<AiProcessingChoice>();
}
