namespace MoAI.App.Queries.Responses;

/// <summary>
/// 会话项.
/// </summary>
public class AppSessionItem
{
    /// <summary>
    /// 会话 id.
    /// </summary>
    public Guid SessionId { get; set; }

    /// <summary>
    /// 所属应用 id.
    /// </summary>
    public Guid AppId { get; set; }

    /// <summary>
    /// 会话标题.
    /// </summary>
    public string Title { get; set; } = default!;

    /// <summary>
    /// 会话绑定的专家提示词 id，0 表示未绑定.
    /// </summary>
    public int PromptId { get; set; }

    /// <summary>
    /// 发起用户类型，对齐 MoAI.Infra.Models.UserType.
    /// </summary>
    public int UserType { get; set; }

    /// <summary>
    /// 输入 token 累计.
    /// </summary>
    public int InputTokens { get; set; }

    /// <summary>
    /// 输出 token 累计.
    /// </summary>
    public int OutTokens { get; set; }

    /// <summary>
    /// token 累计总数.
    /// </summary>
    public int TotalTokens { get; set; }

    /// <summary>
    /// 最后一条消息时间.
    /// </summary>
    public DateTimeOffset LastMessageTime { get; set; }

    /// <summary>
    /// 创建时间.
    /// </summary>
    public DateTimeOffset CreateTime { get; set; }
}
