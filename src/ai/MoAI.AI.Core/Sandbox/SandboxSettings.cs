namespace MoAI.AI.Models;

/// <summary>
/// 应用沙箱配置（对应 <c>app_agent_config.execution_settings.sandbox</c>）.
/// <para>写入 JSON 对象而非独立数据库列，便于后续扩展更多执行能力而无需改表.</para>
/// </summary>
public sealed class SandboxSettings
{
    /// <summary>
    /// 是否启用沙箱；关闭时应用不暴露任何沙箱工具.
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// 沙箱存活时间（秒）覆盖；为空则使用全局默认值.
    /// </summary>
    public int? TimeoutSeconds { get; set; }

    /// <summary>
    /// 剩余存活时间不足阈值时是否自动续期，默认 true.
    /// </summary>
    public bool RenewOnAccess { get; set; } = true;

    /// <summary>
    /// 资源限制覆盖（cpu/memory），为空则使用服务端默认.
    /// </summary>
    public SandboxResourceSettings? Resource { get; set; }

    /// <summary>
    /// 出站网络策略，为空则使用服务端默认.
    /// </summary>
    public SandboxNetworkSettings? Network { get; set; }
}

/// <summary>
/// 沙箱资源限制.
/// </summary>
public sealed class SandboxResourceSettings
{
    /// <summary>
    /// CPU 限制，如 "1" 或 "500m".
    /// </summary>
    public string? Cpu { get; set; }

    /// <summary>
    /// 内存限制，如 "512Mi" 或 "2Gi".
    /// </summary>
    public string? Memory { get; set; }
}

/// <summary>
/// 沙箱出站网络策略.
/// </summary>
public sealed class SandboxNetworkSettings
{
    /// <summary>
    /// 默认动作：allow / deny；为空则由服务端决定.
    /// </summary>
    public string? DefaultAction { get; set; }

    /// <summary>
    /// 例外域名列表：默认 deny 时为放行名单，默认 allow 时为拒绝名单.
    /// </summary>
    public IReadOnlyList<string> Egress { get; set; } = [];
}

