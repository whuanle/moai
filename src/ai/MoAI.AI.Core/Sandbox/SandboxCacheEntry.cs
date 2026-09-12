using System;

namespace MoAI.AI.Models;

/// <summary>
/// 会话沙箱映射缓存（Redis），记录远端沙箱 id 与到期时间.
/// </summary>
public sealed class SandboxCacheEntry
{
    /// <summary>
    /// 远端沙箱 id.
    /// </summary>
    public string SandboxId { get; set; } = string.Empty;

    /// <summary>
    /// 沙箱到期时间（服务端 TTL）.
    /// </summary>
    public DateTimeOffset ExpiresAt { get; set; }
}
