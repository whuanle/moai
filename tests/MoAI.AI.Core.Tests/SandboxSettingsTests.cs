using System.Text.Json;
using MoAI.AI.Models;
using Xunit;

namespace MoAI.AI.Core.Tests;

/// <summary>
/// 执行参数中沙箱配置的解析.
/// </summary>
public class SandboxSettingsTests
{
    [Fact]
    public void Parse_NoSandbox_ReturnsNull()
    {
        var settings = AppAgentExecutionSettings.Parse("{\"preserveTurns\":3}");

        Assert.Null(settings.Sandbox);
    }

    [Fact]
    public void Parse_SandboxEnabled_ReadsFields()
    {
        var settings = AppAgentExecutionSettings.Parse("""
            {"sandbox":{"enabled":true,"timeoutSeconds":120,"renewOnAccess":false}}
            """);

        Assert.NotNull(settings.Sandbox);
        Assert.True(settings.Sandbox!.Enabled);
        Assert.Equal(120, settings.Sandbox.TimeoutSeconds);
        Assert.False(settings.Sandbox.RenewOnAccess);
    }

    [Fact]
    public void Parse_SandboxResourceAndNetwork_ReadsFields()
    {
        var settings = AppAgentExecutionSettings.Parse("""
            {"sandbox":{"enabled":true,"resource":{"cpu":"1","memory":"2Gi"},"network":{"defaultAction":"deny","egress":["pypi.org"]}}}
            """);

        Assert.Equal("1", settings.Sandbox!.Resource!.Cpu);
        Assert.Equal("2Gi", settings.Sandbox.Resource.Memory);
        Assert.Equal("deny", settings.Sandbox.Network!.DefaultAction);
        Assert.Contains("pypi.org", settings.Sandbox.Network.Egress);
    }

    [Fact]
    public void Parse_SandboxEnabled_DefaultsRenewOnAccessTrue()
    {
        var settings = AppAgentExecutionSettings.Parse("{\"sandbox\":{\"enabled\":true}}");

        Assert.True(settings!.Sandbox!.RenewOnAccess);
    }

    [Fact]
    public void Parse_NetworkRules_ReadsFields()
    {
        var settings = AppAgentExecutionSettings.Parse("""
            {"sandbox":{"enabled":true,"network":{"defaultAction":"deny","rules":[
                {"action":"allow","target":"pypi.org"},
                {"action":"deny","target":"10.0.0.0/8","ports":[443,80]}
            ]}}}
            """);

        var network = settings.Sandbox!.Network!;
        Assert.Equal(2, network.Rules.Count);
        Assert.Equal("allow", network.Rules[0].Action);
        Assert.Equal("pypi.org", network.Rules[0].Target);
        Assert.Null(network.Rules[0].Ports);
        Assert.Equal("deny", network.Rules[1].Action);
        Assert.Equal([443, 80], network.Rules[1].Ports);
    }

    [Fact]
    public void GetEffectiveRules_LegacyEgress_DefaultDeny_ConvertsToAllowList()
    {
        var network = new SandboxNetworkSettings
        {
            DefaultAction = "deny",
            Egress = ["pypi.org", "*.github.com"]
        };

        var rules = network.GetEffectiveRules();

        Assert.Equal(2, rules.Count);
        Assert.All(rules, x => Assert.Equal("allow", x.Action));
        Assert.Equal("pypi.org", rules[0].Target);
    }

    [Fact]
    public void GetEffectiveRules_LegacyEgress_DefaultAllow_ConvertsToDenyList()
    {
        var network = new SandboxNetworkSettings
        {
            DefaultAction = "allow",
            Egress = ["evil.example.com"]
        };

        var rules = network.GetEffectiveRules();

        Assert.All(rules, x => Assert.Equal("deny", x.Action));
    }

    [Fact]
    public void GetEffectiveDefaultAction_BlankWithRules_ReturnsAllow()
    {
        // 修复黑名单语义：默认动作留空且有规则时显式按 allow 下发（服务端缺省为 deny）
        var network = new SandboxNetworkSettings
        {
            Rules = [new SandboxNetworkRule { Action = "deny", Target = "evil.example.com" }]
        };

        Assert.Equal("allow", network.GetEffectiveDefaultAction());
        Assert.Null(new SandboxNetworkSettings().GetEffectiveDefaultAction());
        Assert.Equal("deny", new SandboxNetworkSettings { DefaultAction = "deny" }.GetEffectiveDefaultAction());
    }

    [Fact]
    public void GetEffectiveRules_RulesPriorityOverLegacyEgress()
    {
        var network = new SandboxNetworkSettings
        {
            DefaultAction = "deny",
            Egress = ["legacy.example.com"],
            Rules = [new SandboxNetworkRule { Action = "allow", Target = "new.example.com" }]
        };

        var rules = network.GetEffectiveRules();

        Assert.Single(rules);
        Assert.Equal("new.example.com", rules[0].Target);
    }

    [Fact]
    public void Parse_InvalidJson_ReturnsDefault()
    {
        var settings = AppAgentExecutionSettings.Parse("{ not json");

        Assert.Null(settings.Sandbox);
        Assert.Equal(16, settings.ToolResultTriggerMessages);
    }
}
