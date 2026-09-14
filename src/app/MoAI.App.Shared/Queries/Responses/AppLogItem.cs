using MoAI.Infra.Models;

namespace MoAI.App.Queries.Responses;

/// <summary>
/// 应用对话日志条目（正式会话的压缩后视图）.
/// </summary>
public class AppLogItem : AuditsInfo
{
    /// <summary>会话 id.</summary>
    public Guid SessionId { get; set; }

    /// <summary>会话标题.</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>会话归属用户类型.</summary>
    public UserType UserType { get; set; }

    /// <summary>会话归属用户原始 id（内部用户为 user.id，外部用户为 external.id）.</summary>
    public long OwnerId { get; set; }

    /// <summary>输入 token.</summary>
    public int InputTokens { get; set; }

    /// <summary>输出 token.</summary>
    public int OutTokens { get; set; }

    /// <summary>合计 token.</summary>
    public int TotalTokens { get; set; }

    /// <summary>最后消息时间.</summary>
    public DateTimeOffset LastMessageTime { get; set; }
}
