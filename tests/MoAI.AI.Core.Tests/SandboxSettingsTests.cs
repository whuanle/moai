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
    public void Parse_InvalidJson_ReturnsDefault()
    {
        var settings = AppAgentExecutionSettings.Parse("{ not json");

        Assert.Null(settings.Sandbox);
        Assert.Equal(12, settings.ToolResultTriggerMessages);
    }
}
