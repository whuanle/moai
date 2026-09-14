namespace MoAI.AI.Models;

/// <summary>
/// 调试会话注册项：记录临时调试会话归属的应用与用户；仅存 Redis，不落库.
/// </summary>
public class DebugSessionRegistryEntry
{
    /// <summary>
    /// 应用 id.
    /// </summary>
    public Guid AppId { get; set; }

    /// <summary>
    /// 团队 id.
    /// </summary>
    public int TeamId { get; set; }

    /// <summary>
    /// 发起调试的用户 id.
    /// </summary>
    public long UserId { get; set; }

    /// <summary>
    /// 创建时间.
    /// </summary>
    public DateTimeOffset CreateTime { get; set; }
}
