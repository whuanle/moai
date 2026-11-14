using MoAI.AI.Models;
using MoAI.Settings.Models;
using OpenSandbox.Models;

namespace MoAI.AI.Services;

/// <summary>
/// 沙箱出站网络策略构建器：把应用配置（<see cref="SandboxNetworkSettings"/>）与平台管控模式（<see cref="SandboxLimitsSettings.NetworkMode"/>）
/// 归一为 OpenSandbox <see cref="NetworkPolicy"/>.
/// <para>规则：deny / intranet 模式强制默认拒绝（应用仅可追加放行规则，intranet 另自动放行内网网段）；
/// app 模式沿用应用配置，defaultAction 留空且有规则时显式按 allow（黑名单）下发，避免服务端默认 deny 把黑名单变成全禁.</para>
/// </summary>
internal static class SandboxNetworkPolicyBuilder
{
    /// <summary>
    /// 构建网络策略；无需下发（app 模式且未配置）返回 null.
    /// </summary>
    /// <param name="network">应用出站网络配置，可为空.</param>
    /// <param name="limits">系统沙箱设置（含管控模式与内网网段）.</param>
    /// <returns>策略或 null.</returns>
    public static NetworkPolicy? Build(SandboxNetworkSettings? network, SandboxLimitsSettings limits)
    {
        var mode = NetworkModes.IsValid(limits.NetworkMode) ? limits.NetworkMode : NetworkModes.App;
        var governed = mode == NetworkModes.Deny || mode == NetworkModes.Intranet;

        var rules = network?.GetEffectiveRules() ?? [];

        string? defaultAction;
        if (governed)
        {
            // 平台管控：默认动作一律拒绝，应用规则仅作为追加放行 / 收紧条目
            defaultAction = NetworkRuleAction.Deny.ToString().ToLowerInvariant();
        }
        else if (network == null)
        {
            return null;
        }
        else
        {
            defaultAction = network.GetEffectiveDefaultAction();
        }

        var egress = rules
            .Select(MapRule)
            .ToList();

        if (mode == NetworkModes.Intranet)
        {
            // 注入内网网段放行，置于规则列表前部；与应用已有同目标 allow 规则去重
            var allowedTargets = egress
                .Where(x => x.Action == NetworkRuleAction.Allow && x.Target != null)
                .Select(x => x.Target!)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            foreach (var cidr in limits.IntranetCidrs)
            {
                if (allowedTargets.Add(cidr))
                {
                    egress.Insert(0, new NetworkRule { Action = NetworkRuleAction.Allow, Target = cidr });
                }
            }
        }

        if (defaultAction == null && egress.Count == 0)
        {
            return null;
        }

        return new NetworkPolicy
        {
            DefaultAction = ParseAction(defaultAction),
            Egress = egress,
        };
    }

    private static NetworkRule MapRule(SandboxNetworkRule rule)
    {
        var ports = rule.Ports != null && rule.Ports.Count > 0 ? rule.Ports.ToList() : null;
        return new NetworkRule
        {
            Action = ParseAction(rule.Action) ?? NetworkRuleAction.Deny,
            // 空目标序列化时省略，规则退化为纯端口规则（SDK 与服务端语义一致）
            Target = string.IsNullOrWhiteSpace(rule.Target) ? null : rule.Target.Trim(),
            Ports = ports,
        };
    }

    private static NetworkRuleAction? ParseAction(string? action)
        => action?.Trim().ToLowerInvariant() switch
        {
            "allow" => NetworkRuleAction.Allow,
            "deny" => NetworkRuleAction.Deny,
            _ => null
        };
}
