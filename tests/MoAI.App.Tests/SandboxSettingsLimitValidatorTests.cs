using System.Text.Json;
using MoAI.App.Validation;
using MoAI.Infra.Exceptions;
using MoAI.Settings.Models;
using Xunit;

namespace MoAI.App.Tests;

/// <summary>
/// 应用执行参数中沙箱配置的上限校验.
/// </summary>
public class SandboxSettingsLimitValidatorTests
{
    private static readonly SandboxLimitsSettings Limits = new()
    {
        MaxTtlSeconds = 3600,
        MaxCpu = "2",
        MaxMemory = "2Gi",
        MaxCpuMillicores = 2000,
        MaxMemoryBytes = 2L * 1024 * 1024 * 1024
    };

    private static readonly SandboxLimitsSettings GovernedLimits = new()
    {
        MaxTtlSeconds = 3600,
        MaxCpu = "2",
        MaxMemory = "2Gi",
        MaxCpuMillicores = 2000,
        MaxMemoryBytes = 2L * 1024 * 1024 * 1024,
        NetworkMode = NetworkModes.Intranet,
        IntranetCidrs = ["10.0.0.0/8"]
    };

    [Fact]
    public void Validate_WithinLimits_Passes()
    {
        var settings = Parse("""{"sandbox":{"enabled":true,"timeoutSeconds":3600,"resource":{"cpu":"2","memory":"2Gi"}}}""");

        SandboxSettingsLimitValidator.Validate(settings, Limits);
    }

    [Fact]
    public void Validate_TimeoutOverLimit_Throws400()
    {
        var settings = Parse("""{"sandbox":{"enabled":true,"timeoutSeconds":3601}}""");

        var ex = Assert.Throws<BusinessException>(() => SandboxSettingsLimitValidator.Validate(settings, Limits));
        Assert.Equal(400, ex.StatusCode);
        Assert.Contains("3600", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Validate_CpuOverLimit_Throws400()
    {
        var settings = Parse("""{"sandbox":{"enabled":true,"resource":{"cpu":"2500m"}}}""");

        var ex = Assert.Throws<BusinessException>(() => SandboxSettingsLimitValidator.Validate(settings, Limits));
        Assert.Equal(400, ex.StatusCode);
    }

    [Fact]
    public void Validate_MemoryOverLimit_Throws400()
    {
        var settings = Parse("""{"sandbox":{"enabled":true,"resource":{"memory":"4Gi"}}}""");

        var ex = Assert.Throws<BusinessException>(() => SandboxSettingsLimitValidator.Validate(settings, Limits));
        Assert.Equal(400, ex.StatusCode);
    }

    [Fact]
    public void Validate_InvalidQuantity_Throws400()
    {
        var settings = Parse("""{"sandbox":{"enabled":true,"resource":{"cpu":"fast"}}}""");

        var ex = Assert.Throws<BusinessException>(() => SandboxSettingsLimitValidator.Validate(settings, Limits));
        Assert.Equal(400, ex.StatusCode);
        Assert.Contains("格式无效", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Validate_SandboxDisabled_SkipsChecks()
    {
        var settings = Parse("""{"sandbox":{"enabled":false,"timeoutSeconds":999999,"resource":{"cpu":"99","memory":"999Gi"}}}""");

        SandboxSettingsLimitValidator.Validate(settings, Limits);
    }

    [Fact]
    public void Validate_NoSandboxOrOverlays_Passes()
    {
        SandboxSettingsLimitValidator.Validate(Parse("{}"), Limits);
        SandboxSettingsLimitValidator.Validate(Parse("""{"preserveTurns":3}"""), Limits);
    }

    [Fact]
    public void Validate_TimeoutNotPositive_SkipsCheck()
    {
        // 非正数表示回落服务端默认值，不按超限处理
        var settings = Parse("""{"sandbox":{"enabled":true,"timeoutSeconds":0}}""");

        SandboxSettingsLimitValidator.Validate(settings, Limits);
    }

    [Fact]
    public void Validate_EmptyResourceValues_Pass()
    {
        // 留空表示使用服务端默认
        var settings = Parse("""{"sandbox":{"enabled":true,"resource":{"cpu":"","memory":""}}}""");

        SandboxSettingsLimitValidator.Validate(settings, Limits);
    }

    [Fact]
    public void Validate_NetworkRules_Pass()
    {
        var settings = Parse("""
            {"sandbox":{"enabled":true,"network":{"defaultAction":"deny","rules":[
                {"action":"allow","target":"pypi.org"},
                {"action":"allow","target":"*.github.com"},
                {"action":"allow","target":"10.0.0.0/8","ports":[443,80]},
                {"action":"deny","target":"169.254.169.254"}
            ]}}}
            """);

        SandboxSettingsLimitValidator.Validate(settings, Limits);
    }

    [Fact]
    public void Validate_LegacyEgress_Pass()
    {
        var settings = Parse("""{"sandbox":{"enabled":true,"network":{"defaultAction":"deny","egress":["pypi.org","10.0.0.0/8"]}}}""");

        SandboxSettingsLimitValidator.Validate(settings, Limits);
    }

    [Fact]
    public void Validate_InvalidTarget_Throws400()
    {
        var settings = Parse("""{"sandbox":{"enabled":true,"network":{"defaultAction":"deny","rules":[{"action":"allow","target":"not a domain!"}]}}}""");

        var ex = Assert.Throws<BusinessException>(() => SandboxSettingsLimitValidator.Validate(settings, Limits));
        Assert.Equal(400, ex.StatusCode);
        Assert.Contains("目标格式无效", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Validate_InvalidDefaultAction_Throws400()
    {
        var settings = Parse("""{"sandbox":{"enabled":true,"network":{"defaultAction":"reject"}}}""");

        var ex = Assert.Throws<BusinessException>(() => SandboxSettingsLimitValidator.Validate(settings, Limits));
        Assert.Equal(400, ex.StatusCode);
    }

    [Fact]
    public void Validate_PortsOnDomainTarget_Throws400()
    {
        var settings = Parse("""{"sandbox":{"enabled":true,"network":{"defaultAction":"deny","rules":[{"action":"allow","target":"pypi.org","ports":[443]}]}}}""");

        var ex = Assert.Throws<BusinessException>(() => SandboxSettingsLimitValidator.Validate(settings, Limits));
        Assert.Equal(400, ex.StatusCode);
        Assert.Contains("端口", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Validate_InvalidPorts_Throws400()
    {
        var settings = Parse("""{"sandbox":{"enabled":true,"network":{"defaultAction":"deny","rules":[{"action":"allow","target":"10.0.0.0/8","ports":[0,443]}]}}}""");

        var ex = Assert.Throws<BusinessException>(() => SandboxSettingsLimitValidator.Validate(settings, Limits));
        Assert.Equal(400, ex.StatusCode);
    }

    [Fact]
    public void Validate_DuplicatePorts_Throws400()
    {
        var settings = Parse("""{"sandbox":{"enabled":true,"network":{"defaultAction":"deny","rules":[{"action":"allow","target":"10.0.0.0/8","ports":[443,443]}]}}}""");

        Assert.Throws<BusinessException>(() => SandboxSettingsLimitValidator.Validate(settings, Limits));
    }

    [Fact]
    public void Validate_TooManyRules_Throws400()
    {
        var rules = string.Join(
            ",",
            Enumerable.Range(0, 101).Select(i => $"{{\"action\":\"allow\",\"target\":\"host{i}.example.com\"}}"));
        var json = "{\"sandbox\":{\"enabled\":true,\"network\":{\"defaultAction\":\"deny\",\"rules\":[" + rules + "]}}}";
        var settings = Parse(json);

        var ex = Assert.Throws<BusinessException>(() => SandboxSettingsLimitValidator.Validate(settings, Limits));
        Assert.Equal(400, ex.StatusCode);
        Assert.Contains("100", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Validate_GovernedMode_DefaultAllow_Throws400()
    {
        var settings = Parse("""{"sandbox":{"enabled":true,"network":{"defaultAction":"allow","egress":["evil.example.com"]}}}""");

        var ex = Assert.Throws<BusinessException>(() => SandboxSettingsLimitValidator.Validate(settings, GovernedLimits));
        Assert.Equal(400, ex.StatusCode);
        Assert.Contains("默认拒绝", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Validate_GovernedMode_DefaultDeny_Passes()
    {
        var settings = Parse("""{"sandbox":{"enabled":true,"network":{"defaultAction":"deny","rules":[{"action":"allow","target":"pypi.org"}]}}}""");

        SandboxSettingsLimitValidator.Validate(settings, GovernedLimits);
    }

    [Fact]
    public void Validate_CommandFilter_Pass()
    {
        var settings = Parse("""
            {"sandbox":{"enabled":true,"commandFilter":{"enabled":true,"deny":["rm","shutdown","regex:\\b(wget|curl)\\b"]}}}
            """);

        SandboxSettingsLimitValidator.Validate(settings, Limits);
    }

    [Fact]
    public void Validate_CommandFilterInvalidRegex_Throws400()
    {
        var settings = Parse("""{"sandbox":{"enabled":true,"commandFilter":{"enabled":true,"deny":["regex:([bad"]}}}""");

        var ex = Assert.Throws<BusinessException>(() => SandboxSettingsLimitValidator.Validate(settings, Limits));
        Assert.Equal(400, ex.StatusCode);
        Assert.Contains("无效", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Validate_CommandFilterTooManyPatterns_Throws400()
    {
        var deny = string.Join(",", Enumerable.Range(0, 101).Select(i => $"\"cmd{i}\""));
        var json = "{\"sandbox\":{\"enabled\":true,\"commandFilter\":{\"enabled\":true,\"deny\":[" + deny + "]}}}";
        var settings = Parse(json);

        var ex = Assert.Throws<BusinessException>(() => SandboxSettingsLimitValidator.Validate(settings, Limits));
        Assert.Equal(400, ex.StatusCode);
        Assert.Contains("100", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Validate_CommandFilterDenyMustBeStringArray_Throws400()
    {
        var settings = Parse("""{"sandbox":{"enabled":true,"commandFilter":{"enabled":true,"deny":[1,2]}}}""");

        var ex = Assert.Throws<BusinessException>(() => SandboxSettingsLimitValidator.Validate(settings, Limits));
        Assert.Equal(400, ex.StatusCode);
    }

    [Fact]
    public void Validate_CommandFilterEnabledMustBeBool_Throws400()
    {
        var settings = Parse("""{"sandbox":{"enabled":true,"commandFilter":{"enabled":"yes"}}}""");

        var ex = Assert.Throws<BusinessException>(() => SandboxSettingsLimitValidator.Validate(settings, Limits));
        Assert.Equal(400, ex.StatusCode);
    }

    private static JsonElement Parse(string json)
        => JsonDocument.Parse(json).RootElement.Clone();
}
