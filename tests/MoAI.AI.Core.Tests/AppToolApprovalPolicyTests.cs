using MoAI.AI;
using Xunit;

namespace MoAI.AI.Core.Tests;

/// <summary>
/// AppToolApprovalPolicy（execution_settings.toolApproval 节）的解析与放行判定.
/// </summary>
public class AppToolApprovalPolicyTests
{
    private static readonly Guid PluginA = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid PluginB = Guid.Parse("22222222-2222-2222-2222-222222222222");

    [Fact]
    public void Parse_FullNode_ReadsSandboxFlagAndPluginWhitelist()
    {
        var json = $$"""
        {
          "sandbox": { "enabled": true },
          "toolApproval": {
            "autoApprovePlugins": ["{{PluginA}}", "{{PluginB}}"],
            "sandboxAutoApproved": true
          }
        }
        """;

        var policy = AppToolApprovalPolicy.Parse(json);

        Assert.True(policy.SandboxAutoApproved);
        Assert.Equal([PluginA, PluginB], policy.AutoApprovePlugins);
    }

    [Fact]
    public void Parse_MissingNode_ReturnsEmpty()
    {
        var policy = AppToolApprovalPolicy.Parse("""{"sandbox":{"enabled":true}}""");

        Assert.Same(AppToolApprovalPolicy.Empty, policy);
        Assert.False(policy.SandboxAutoApproved);
        Assert.Empty(policy.AutoApprovePlugins);
    }

    [Fact]
    public void Parse_BlankOrInvalidJson_ReturnsEmpty()
    {
        Assert.Same(AppToolApprovalPolicy.Empty, AppToolApprovalPolicy.Parse(null));
        Assert.Same(AppToolApprovalPolicy.Empty, AppToolApprovalPolicy.Parse("   "));
        Assert.Same(AppToolApprovalPolicy.Empty, AppToolApprovalPolicy.Parse("{not json"));
    }

    [Fact]
    public void Parse_InvalidEntries_SkippedAndDeduplicated()
    {
        var json = $$"""
        {
          "toolApproval": {
            "autoApprovePlugins": ["{{PluginA}}", "{{PluginA}}", "not-a-guid", "00000000-0000-0000-0000-000000000000", 42]
          }
        }
        """;

        var policy = AppToolApprovalPolicy.Parse(json);

        Assert.Equal([PluginA], policy.AutoApprovePlugins);
    }

    [Fact]
    public void IsAutoApproved_SandboxFlagMatchesSandboxKindOnly()
    {
        var policy = new AppToolApprovalPolicy { SandboxAutoApproved = true };

        Assert.True(policy.IsAutoApproved("sandbox", null));
        Assert.True(policy.IsAutoApproved("sandbox", PluginA));
        // 沙箱开关只放行沙箱类工具，插件/流程类工具不受影响
        Assert.False(policy.IsAutoApproved("static", null));
        Assert.False(policy.IsAutoApproved("workflow", PluginB));
    }

    [Fact]
    public void IsAutoApproved_PluginWhitelistMatchesSourceId()
    {
        var policy = new AppToolApprovalPolicy { AutoApprovePlugins = [PluginA] };

        Assert.True(policy.IsAutoApproved("static", PluginA));
        Assert.True(policy.IsAutoApproved("dynamic", PluginA));
        Assert.True(policy.IsAutoApproved("mcp", PluginA));
        Assert.False(policy.IsAutoApproved("static", PluginB));
        Assert.False(policy.IsAutoApproved("static", null));
    }

    [Fact]
    public void IsAutoApproved_EmptyPolicy_AutoApprovesNothing()
    {
        Assert.False(AppToolApprovalPolicy.Empty.IsAutoApproved("sandbox", null));
        Assert.False(AppToolApprovalPolicy.Empty.IsAutoApproved("mcp", PluginA));
    }
}
