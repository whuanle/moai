using MoAI.AI.Services;
using MoAI.AI.Models;
using MoAI.Settings.Models;
using OpenSandbox.Models;
using Xunit;

namespace MoAI.AI.Core.Tests;

/// <summary>
/// 沙箱出站网络策略构建：应用配置 + 平台管控模式 → OpenSandbox NetworkPolicy.
/// </summary>
public class SandboxNetworkPolicyBuilderTests
{
    private static SandboxLimitsSettings Limits(string mode = NetworkModes.App, IReadOnlyList<string>? cidrs = null)
        => new()
        {
            MaxTtlSeconds = 3600,
            MaxCpu = "2",
            MaxMemory = "2Gi",
            MaxCpuMillicores = 2000,
            MaxMemoryBytes = 2L * 1024 * 1024 * 1024,
            NetworkMode = mode,
            IntranetCidrs = cidrs ?? []
        };

    [Fact]
    public void Build_AppMode_NoNetwork_ReturnsNull()
    {
        Assert.Null(SandboxNetworkPolicyBuilder.Build(null, Limits()));
        Assert.Null(SandboxNetworkPolicyBuilder.Build(new SandboxNetworkSettings(), Limits()));
    }

    [Fact]
    public void Build_AppMode_BlankDefaultActionWithRules_ForcesAllow()
    {
        // 黑名单语义修复：defaultAction 留空时服务端默认 deny 会把黑名单变成全禁
        var network = new SandboxNetworkSettings
        {
            Rules = [new SandboxNetworkRule { Action = "deny", Target = "evil.example.com" }]
        };

        var policy = SandboxNetworkPolicyBuilder.Build(network, Limits());

        Assert.NotNull(policy);
        Assert.Equal(NetworkRuleAction.Allow, policy!.DefaultAction);
        Assert.Single(policy.Egress);
        Assert.Equal(NetworkRuleAction.Deny, policy.Egress[0].Action);
        Assert.Equal("evil.example.com", policy.Egress[0].Target);
    }

    [Fact]
    public void Build_AppMode_PerRuleActionAndPorts_Maps()
    {
        var network = new SandboxNetworkSettings
        {
            DefaultAction = "deny",
            Rules =
            [
                new SandboxNetworkRule { Action = "allow", Target = "10.0.0.0/8", Ports = [443, 80] },
                new SandboxNetworkRule { Action = "allow", Target = "pypi.org" }
            ]
        };

        var policy = SandboxNetworkPolicyBuilder.Build(network, Limits());

        Assert.NotNull(policy);
        Assert.Equal(NetworkRuleAction.Deny, policy!.DefaultAction);
        Assert.Equal(2, policy.Egress.Count);
        Assert.Equal([443, 80], policy.Egress[0].Ports);
        Assert.Null(policy.Egress[1].Ports);
    }

    [Fact]
    public void Build_LegacyEgress_DefaultDeny_MapsToAllowRules()
    {
        var network = new SandboxNetworkSettings
        {
            DefaultAction = "deny",
            Egress = ["pypi.org"]
        };

        var policy = SandboxNetworkPolicyBuilder.Build(network, Limits());

        Assert.NotNull(policy);
        Assert.Equal(NetworkRuleAction.Deny, policy!.DefaultAction);
        Assert.Single(policy.Egress);
        Assert.Equal(NetworkRuleAction.Allow, policy.Egress[0].Action);
        Assert.Equal("pypi.org", policy.Egress[0].Target);
    }

    [Fact]
    public void Build_DenyMode_ForcesDefaultDeny()
    {
        var network = new SandboxNetworkSettings
        {
            DefaultAction = "allow",
            Egress = ["evil.example.com"]
        };

        var policy = SandboxNetworkPolicyBuilder.Build(network, Limits(NetworkModes.Deny));

        Assert.NotNull(policy);
        Assert.Equal(NetworkRuleAction.Deny, policy!.DefaultAction);
        // allow 名单在强制拒绝下退化为拒绝条目
        Assert.Equal(NetworkRuleAction.Deny, policy.Egress[0].Action);
    }

    [Fact]
    public void Build_DenyMode_NoNetwork_StillForcesDeny()
    {
        var policy = SandboxNetworkPolicyBuilder.Build(null, Limits(NetworkModes.Deny));

        Assert.NotNull(policy);
        Assert.Equal(NetworkRuleAction.Deny, policy!.DefaultAction);
        Assert.Empty(policy.Egress);
    }

    [Fact]
    public void Build_IntranetMode_InjectsIntranetCidrs()
    {
        var policy = SandboxNetworkPolicyBuilder.Build(null, Limits(NetworkModes.Intranet, ["10.0.0.0/8", "172.16.0.0/12"]));

        Assert.NotNull(policy);
        Assert.Equal(NetworkRuleAction.Deny, policy!.DefaultAction);
        Assert.Equal(2, policy.Egress.Count);
        Assert.All(policy.Egress, x => Assert.Equal(NetworkRuleAction.Allow, x.Action));
    }

    [Fact]
    public void Build_IntranetMode_DedupesWithAppAllowRules()
    {
        var network = new SandboxNetworkSettings
        {
            Rules = [new SandboxNetworkRule { Action = "allow", Target = "10.0.0.0/8" }]
        };

        var policy = SandboxNetworkPolicyBuilder.Build(network, Limits(NetworkModes.Intranet, ["10.0.0.0/8", "192.168.0.0/16"]));

        Assert.NotNull(policy);
        // 10.0.0.0/8 应用已声明，不重复注入；只追加 192.168.0.0/16
        Assert.Single(policy.Egress.Where(x => x.Target == "10.0.0.0/8"));
        Assert.Equal(2, policy.Egress.Count);
    }

    [Fact]
    public void Build_IntranetMode_KeepsAppAllowDomains()
    {
        var network = new SandboxNetworkSettings
        {
            Rules = [new SandboxNetworkRule { Action = "allow", Target = "pypi.org" }]
        };

        var policy = SandboxNetworkPolicyBuilder.Build(network, Limits(NetworkModes.Intranet, ["10.0.0.0/8"]));

        Assert.NotNull(policy);
        Assert.Equal(2, policy.Egress.Count);
        Assert.Contains(policy.Egress, x => x.Target == "pypi.org" && x.Action == NetworkRuleAction.Allow);
    }
}
