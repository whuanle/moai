namespace MoAI.App.Apps.CommonApp.Responses;

/// <summary>
/// 应用对话列表响应.
/// </summary>
public class QueryAppChatTopicListCommandResponse
{
    /// <summary>
    /// 对话列表.
    /// </summary>
    public IReadOnlyCollection<AppChatTopicItem> Items { get; init; } = Array.Empty<AppChatTopicItem>();
}

/// <summary>
/// 对话主题项.
/// </summary>
public class AppChatTopicItem
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
    /// 创建时间.
    /// </summary>
    public DateTimeOffset CreateTime { get; init; }
}
