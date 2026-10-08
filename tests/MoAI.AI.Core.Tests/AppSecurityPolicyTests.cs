using System.Text.Json.Nodes;
using MoAI.Database.Aggregates;
using MoAI.Database.Entities;
using Xunit;

namespace MoAI.AI.Core.Tests;

/// <summary>
/// AppSecurityPolicy 脱敏聚合：规则解析/内置正则/范围门控/JSON 脱敏.
/// </summary>
public class AppSecurityPolicyTests
{
    private static AppSecurityConfigEntity CreateEntity(bool enabled = true, bool toolResult = true, bool toolArgs = true, bool modelOutput = true, string rules = """
        [{"name":"手机号","type":"phone"},{"name":"证件","type":"idCard"},{"name":"邮箱","type":"email"},{"name":"卡号","type":"bankCard"}]
        """, string? modelRules = null)
    {
        return new AppSecurityConfigEntity
        {
            Id = Guid.CreateVersion7(),
            TeamId = 1,
            AppId = Guid.CreateVersion7(),
            Enabled = enabled,
            MaskToolResult = toolResult,
            MaskToolArgs = toolArgs,
            MaskModelOutput = modelOutput,
            Rules = rules,
            ModelOutputRules = modelRules ?? "[]",
        };
    }

    [Fact]
    public void Parse_DisabledOrNull_ReturnsDisabledPolicy()
    {
        Assert.False(AppSecurityPolicy.Parse(null).IsActive);
        Assert.False(AppSecurityPolicy.Parse(CreateEntity(enabled: false)).IsActive);
        Assert.False(AppSecurityPolicy.Parse(CreateEntity(rules: "[]")).IsActive);
        Assert.False(AppSecurityPolicy.Parse(CreateEntity(rules: "not-json")).IsActive);
    }

    [Fact]
    public void Parse_ModelRulesWorkIndependentlyOfEnabledAndContentRules()
    {
        // 总开关关闭但模型回复开启且配了模型规则：模型脱敏生效，内容规则不生效
        var policy = AppSecurityPolicy.Parse(CreateEntity(
            enabled: false,
            modelOutput: true,
            rules: "[]",
            modelRules: """[{"name":"手机号","type":"phone"}]"""));

        Assert.True(policy.IsActive);
        Assert.False(policy.ContentMaskActive);
        Assert.True(policy.ModelMaskActive);
        Assert.Equal("工具 13812345678", policy.MaskToolResultText("工具 13812345678"));
        Assert.Equal("回复 ***", policy.MaskModelText("回复 13812345678"));
    }

    [Fact]
    public void Parse_TwoRuleSetsDoNotInterfere()
    {
        // 内容规则只认订单号、模型规则只认手机号：各作用域按各自规则脱敏
        var policy = AppSecurityPolicy.Parse(CreateEntity(
            rules: """[{"name":"order","type":"custom","pattern":"ORD-\\d{6}","replacement":"[ORDER]"}]""",
            modelRules: """[{"name":"手机号","type":"phone"}]"""));

        Assert.True(policy.ContentMaskActive);
        Assert.True(policy.ModelMaskActive);
        Assert.Equal("{\"order\":\"[ORDER]\",\"phone\":\"13812345678\"}", policy.MaskToolResultText("{\"order\":\"ORD-123456\",\"phone\":\"13812345678\"}"));
        Assert.Equal("回复 ORD-123456 或 ***", policy.MaskModelText("回复 ORD-123456 或 13812345678"));
    }

    [Fact]
    public void Parse_EmptyModelRules_ModelMaskInactive()
    {
        var policy = AppSecurityPolicy.Parse(CreateEntity(modelRules: """[{"name":"坏","type":"custom","pattern":"(["}]"""));
        Assert.True(policy.ContentMaskActive);
        Assert.False(policy.ModelMaskActive);
        Assert.Equal("回复 13812345678", policy.MaskModelText("回复 13812345678"));
    }

    [Fact]
    public void Parse_CustomRuleWithInvalidRegex_SkipsBrokenRuleKeepsOthers()
    {
        var policy = AppSecurityPolicy.Parse(CreateEntity(rules: """[{"name":"坏","type":"custom","pattern":"(["},{"name":"手机","type":"phone"}]"""));
        Assert.True(policy.IsActive);
        Assert.Equal("***", policy.MaskText("手机 13812345678")[^3..]);
        Assert.DoesNotContain("***", policy.MaskText("普通文本"));
    }

    [Fact]
    public void MaskText_BuiltinRules_MaskSensitiveData()
    {
        var policy = AppSecurityPolicy.Parse(CreateEntity());
        Assert.Equal("手机 ***", policy.MaskText("手机 13812345678"));
        Assert.Equal("证件 ***", policy.MaskText("证件 11010119900307867X"));
        Assert.Equal("邮箱 ***", policy.MaskText("邮箱 someone@example.com"));
        Assert.Equal("卡号 ***", policy.MaskText("卡号 6222020200112233445"));
    }

    [Fact]
    public void MaskText_DoesNotMatchPartialDigits()
    {
        var policy = AppSecurityPolicy.Parse(CreateEntity());
        // 前后紧邻数字不命中（避免截断长数字串）；11 位但非 1[3-9] 开头不命中
        Assert.Equal("9138123456781", policy.MaskText("9138123456781"));
        Assert.Equal("01234567890", policy.MaskText("01234567890"));
    }

    [Fact]
    public void MaskText_CustomRule_UsesCustomPatternAndReplacement()
    {
        var policy = AppSecurityPolicy.Parse(CreateEntity(rules: """[{"name":"订单","type":"custom","pattern":"ORD-\\d{6}","replacement":"[订单]"}]"""));
        Assert.Equal("订单 [订单] 已发货", policy.MaskText("订单 ORD-123456 已发货"));
    }

    [Fact]
    public void ScopeGates_OffScope_IsNoOp()
    {
        var resultOff = AppSecurityPolicy.Parse(CreateEntity(toolResult: false));
        Assert.Equal("{\"phone\":\"13812345678\"}", resultOff.MaskToolResultText("{\"phone\":\"13812345678\"}"));

        var argsOff = AppSecurityPolicy.Parse(CreateEntity(toolArgs: false));
        Assert.Equal("{\"phone\":\"13812345678\"}", argsOff.MaskToolArgsText("{\"phone\":\"13812345678\"}"));

        var modelOff = AppSecurityPolicy.Parse(CreateEntity(modelOutput: false));
        Assert.Equal("回复 13812345678", modelOff.MaskModelText("回复 13812345678"));
    }

    [Fact]
    public void MaskJson_HandlesStringAndNumberLiteralsRecursively()
    {
        var policy = AppSecurityPolicy.Parse(CreateEntity());
        var json = JsonNode.Parse("""
            {"phone":"13812345678","order":{"tel":13912345678,"list":[
                {"contact":"13812345678"}
            ]},"note":"无"}
            """)!;
        policy.MaskJson(json);
        Assert.Equal("***", (string)json["phone"]!);
        Assert.Equal("***", (string)json["order"]!["tel"]!);
        Assert.Equal("***", (string)json["order"]!["list"]![0]!["contact"]!);
        Assert.Equal("无", (string)json["note"]!);
    }

    [Fact]
    public void MaskToolResultText_InvalidJson_FallsBackToPlainTextMask()
    {
        var policy = AppSecurityPolicy.Parse(CreateEntity());
        Assert.Equal("错误：***", policy.MaskToolResultText("错误：13812345678"));
    }

    [Fact]
    public void DisabledPolicy_AllMethodsAreIdentity()
    {
        var policy = AppSecurityPolicy.Disabled;
        Assert.Equal("13812345678", policy.MaskText("13812345678"));
        Assert.Equal("{\"a\":1}", policy.MaskToolResultText("{\"a\":1}"));
        var node = JsonNode.Parse("{\"a\":\"13812345678\"}")!;
        policy.MaskJson(node);
        Assert.Equal("13812345678", (string)node["a"]!);
    }

    [Fact]
    public void CompileRule_ValidAndInvalidPatterns()
    {
        Assert.NotNull(AppSecurityPolicy.CompileRule(AppSecurityRuleTypes.Custom, @"1[3-9]\d{9}"));
        Assert.Null(AppSecurityPolicy.CompileRule(AppSecurityRuleTypes.Custom, "(["));
        Assert.Null(AppSecurityPolicy.CompileRule(AppSecurityRuleTypes.Custom, null));
        Assert.Null(AppSecurityPolicy.CompileRule("bad-type", @"\d+"));
    }
}
