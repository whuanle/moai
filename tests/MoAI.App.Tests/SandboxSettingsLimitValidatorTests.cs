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

    private static JsonElement Parse(string json)
        => JsonDocument.Parse(json).RootElement.Clone();
}
