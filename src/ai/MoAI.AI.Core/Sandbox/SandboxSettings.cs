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

    /// <summary>
    /// 命令过滤黑名单（应用级）；运行时与平台级禁用清单取并集后拦截 sandbox_run_shell / sandbox_run_code.
    /// </summary>
    public SandboxCommandFilterSettings? CommandFilter { get; set; }
}

/// <summary>
/// 沙箱命令过滤配置（应用级黑名单）.
/// </summary>
public sealed class SandboxCommandFilterSettings
{
    /// <summary>
    /// 是否启用应用级命令过滤；关闭时仅平台级禁用清单生效.
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// 禁用命令模式列表：默认按全词边界匹配（rm → \brm\b，不误杀 confirm），regex: 前缀为原生正则；大小写不敏感.
    /// </summary>
    public IReadOnlyList<string> Deny { get; set; } = [];
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
/// <para>规则写入 <see cref="Rules"/>（每条独立动作与可选端口）；旧字段 <see cref="Egress"/> 为单向列表，仅保留读取兼容，
/// 读取时经 <see cref="GetEffectiveRules"/> 统一转换为规则（默认 deny 时为放行名单，否则为拒绝名单）.</para>
/// </summary>
public sealed class SandboxNetworkSettings
{
    /// <summary>
    /// 默认动作：allow / deny；为空且有规则时按「允许（黑名单模式）」显式下发，避免服务端默认 deny 把黑名单变成全禁.
    /// </summary>
    public string? DefaultAction { get; set; }

    /// <summary>
    /// 旧版例外目标列表（只读兼容）：默认 deny 时为放行名单，默认 allow 时为拒绝名单.
    /// </summary>
    public IReadOnlyList<string> Egress { get; set; } = [];

    /// <summary>
    /// 出站规则列表：每条含独立动作（allow/deny）、目标（域名 / IP / CIDR）与可选端口（仅 IP / CIDR 目标）.
    /// </summary>
    public IReadOnlyList<SandboxNetworkRule> Rules { get; set; } = [];

    /// <summary>
    /// 获取生效规则：<see cref="Rules"/> 优先，为空时按旧语义转换 <see cref="Egress"/>.
    /// </summary>
    /// <returns>归一化后的规则列表（动作小写，跳过空目标条目）.</returns>
    public IReadOnlyList<SandboxNetworkRule> GetEffectiveRules()
    {
        if (Rules.Count > 0)
        {
            return Rules
                .Where(x => !string.IsNullOrWhiteSpace(x.Target) || (x.Ports?.Count ?? 0) > 0)
                .Select(x => new SandboxNetworkRule
                {
                    Action = MoAI.Settings.Models.SandboxNetworkTarget.NormalizeAction(x.Action) ?? "deny",
                    Target = x.Target?.Trim() ?? string.Empty,
                    Ports = x.Ports,
                })
                .ToList();
        }

        // 旧格式：默认 deny 时列表是放行名单，否则是拒绝名单
        var legacyAction = DefaultAction?.Trim().ToLowerInvariant() == "deny" ? "allow" : "deny";
        return Egress
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => new SandboxNetworkRule { Action = legacyAction, Target = x.Trim() })
            .ToList();
    }

    /// <summary>
    /// 归一化默认动作：留空且有生效规则时返回 allow（黑名单模式），否则返回归一化原文.
    /// </summary>
    /// <returns>allow / deny / null（无策略）.</returns>
    public string? GetEffectiveDefaultAction()
    {
        var normalized = MoAI.Settings.Models.SandboxNetworkTarget.NormalizeAction(DefaultAction);
        if (normalized != null)
        {
            return normalized;
        }

        return GetEffectiveRules().Count > 0 ? "allow" : null;
    }
}

/// <summary>
/// 沙箱出站规则.
/// </summary>
public sealed class SandboxNetworkRule
{
    /// <summary>
    /// 动作：allow / deny.
    /// </summary>
    public string Action { get; set; } = "allow";

    /// <summary>
    /// 目标：域名（含 *. 通配符）、IP 或 CIDR 网段；为空时规则作用于所有目标的端口限制.
    /// </summary>
    public string Target { get; set; } = string.Empty;

    /// <summary>
    /// 可选 TCP 端口列表（1~65535）；仅 IP / CIDR 目标支持（域名目标由服务端拒绝）.
    /// </summary>
    public IReadOnlyList<int>? Ports { get; set; }
}

