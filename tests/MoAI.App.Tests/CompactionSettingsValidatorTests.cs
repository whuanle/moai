using System.Text.Json;
using MoAI.App.Validation;
using MoAI.Infra.Exceptions;
using Xunit;

namespace MoAI.App.Tests;

/// <summary>
/// 应用执行参数中上下文压缩配置的校验：模式枚举与 custom 模式数值限幅.
/// </summary>
public class CompactionSettingsValidatorTests
{
    [Theory]
    [InlineData("auto")]
    [InlineData("custom")]
    public void Validate_ValidModes_Passes(string mode)
    {
        CompactionSettingsValidator.Validate(Parse($$"""{"compactionMode":"{{mode}}"}"""));
    }

    [Fact]
    public void Validate_NoCompactionFields_Passes()
    {
        CompactionSettingsValidator.Validate(Parse("""{"sandbox":{"enabled":false}}"""));
    }

    [Fact]
    public void Validate_InvalidMode_Throws400()
    {
        var ex = Assert.Throws<BusinessException>(() => CompactionSettingsValidator.Validate(Parse("""{"compactionMode":"aggressive"}""")));
        Assert.Equal(400, ex.StatusCode);
    }

    [Fact]
    public void Validate_CustomWithinRange_Passes()
    {
        CompactionSettingsValidator.Validate(Parse("""
            {"compactionMode":"custom","toolResultTriggerMessages":8,"summarizeTriggerTokens":10000,"hardTokenLimit":50000}
            """));
    }

    [Fact]
    public void Validate_CustomSummarizeBelowMin_Throws400()
    {
        var ex = Assert.Throws<BusinessException>(() => CompactionSettingsValidator.Validate(Parse("""{"compactionMode":"custom","summarizeTriggerTokens":100}""")));
        Assert.Equal(400, ex.StatusCode);
    }

    [Fact]
    public void Validate_CustomHardLimitNotGreaterThanSummarize_Throws400()
    {
        var ex = Assert.Throws<BusinessException>(() => CompactionSettingsValidator.Validate(Parse("""{"compactionMode":"custom","summarizeTriggerTokens":10000,"hardTokenLimit":10000}""")));
        Assert.Equal(400, ex.StatusCode);
    }

    [Fact]
    public void Validate_AutoModeWithLegacyNumbers_PassesWithoutValidation()
    {
        // auto 模式绝对值字段不生效、不做校验（历史存量值兼容）
        CompactionSettingsValidator.Validate(Parse("""{"compactionMode":"auto","summarizeTriggerTokens":1,"hardTokenLimit":1}"""));
    }

    private static JsonElement Parse(string json) => JsonDocument.Parse(json).RootElement.Clone();
}
