using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using MoAI.Database.Entities;

namespace MoAI.Database.Aggregates;

/// <summary>
/// 脱敏规则类型代码（app_security_config.rules[].type）.
/// </summary>
public static class AppSecurityRuleTypes
{
    /// <summary>中国大陆手机号.</summary>
    public const string Phone = "phone";

    /// <summary>身份证号（18 位）.</summary>
    public const string IdCard = "idCard";

    /// <summary>电子邮箱.</summary>
    public const string Email = "email";

    /// <summary>银行卡号（16~19 位数字）.</summary>
    public const string BankCard = "bankCard";

    /// <summary>自定义正则（pattern 必填）.</summary>
    public const string Custom = "custom";

    /// <summary>
    /// 全部内置类型代码.
    /// </summary>
    public static readonly string[] Builtin = [Phone, IdCard, Email, BankCard];

    /// <summary>
    /// 全部合法类型代码.
    /// </summary>
    public static readonly string[] All = [Phone, IdCard, Email, BankCard, Custom];

    /// <summary>
    /// 校验类型代码是否合法.
    /// </summary>
    /// <param name="type">类型代码.</param>
    /// <returns>合法返回 true.</returns>
    public static bool IsValid(string? type) => type is not null && Array.IndexOf(All, type) >= 0;
}

/// <summary>
/// 应用内容脱敏策略（app_security_config 聚合）：规则编译与文本/JSON 脱敏执行.
    /// <para>两组规则相互独立维护：<see cref="AppSecurityConfigEntity.Rules"/>（内容规则）作用于工具调用结果（含错误信息、流程节点输出）与工具调用参数（调用记录展示、流程节点输入）；
    /// <see cref="AppSecurityConfigEntity.ModelOutputRules"/>（模型规则）仅作用于模型回复文本，且不依赖内容脱敏总开关.</para>
/// <para>未启用 / 无规则 / 对应范围未开启时，各 Mask 方法原样返回输入.</para>
/// </summary>
public sealed class AppSecurityPolicy
{
    /// <summary>
    /// 默认替换文本.
    /// </summary>
    public const string DefaultReplacement = "***";

    /// <summary>
    /// 最多规则条数（保存入口校验）.
    /// </summary>
    public const int MaxRuleCount = 50;

    private static readonly TimeSpan MatchTimeout = TimeSpan.FromMilliseconds(500);

    private static readonly Dictionary<string, string> BuiltinPatterns = new()
    {
        [AppSecurityRuleTypes.Phone] = @"(?<!\d)1[3-9]\d{9}(?!\d)",
        [AppSecurityRuleTypes.IdCard] = @"(?<!\d)\d{17}[\dXx](?!\d)",
        [AppSecurityRuleTypes.Email] = @"[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\.[A-Za-z]{2,}",
        [AppSecurityRuleTypes.BankCard] = @"(?<!\d)\d{16,19}(?!\d)",
    };

    private static readonly JsonSerializerOptions RuleJsonOptions = new(JsonSerializerDefaults.Web);

    /// <summary>
    /// 未启用脱敏的空策略（各 Mask 方法为恒等函数）.
    /// </summary>
    public static readonly AppSecurityPolicy Disabled = new();

    private readonly List<(Regex Regex, string Replacement)> _contentRules = [];

    private readonly List<(Regex Regex, string Replacement)> _modelRules = [];

    private AppSecurityPolicy()
    {
    }

    /// <summary>
    /// 是否任意一组脱敏生效（内容脱敏或模型回复脱敏）.
    /// </summary>
    public bool IsActive => ContentMaskActive || ModelMaskActive;

    /// <summary>
    /// 内容脱敏是否生效（总开关启用且存在至少一条可编译内容规则）.
    /// </summary>
    public bool ContentMaskActive { get; private set; }

    /// <summary>
    /// 模型回复脱敏是否生效（模型回复范围开启且存在至少一条可编译模型规则；与内容脱敏总开关无关）.
    /// </summary>
    public bool ModelMaskActive { get; private set; }

    /// <summary>
    /// 是否对工具调用结果（含错误信息、流程节点输出）脱敏（含内容脱敏总开关门控）.
    /// </summary>
    public bool ToolResultEnabled { get; private set; }

    /// <summary>
    /// 是否对工具调用参数（调用记录展示、流程节点输入）脱敏（含内容脱敏总开关门控）.
    /// </summary>
    public bool ToolArgsEnabled { get; private set; }

    /// <summary>
    /// 是否对模型回复文本（对话正文）脱敏.
    /// </summary>
    public bool ModelOutputEnabled { get; private set; }

    /// <summary>
    /// 从配置实体解析策略；实体为 null 或两组规则均未生效时返回 <see cref="Disabled"/>.
    /// </summary>
    /// <param name="entity">安全配置实体（可为 null）.</param>
    /// <returns>脱敏策略.</returns>
    public static AppSecurityPolicy Parse(AppSecurityConfigEntity? entity)
    {
        if (entity == null)
        {
            return Disabled;
        }

        var policy = new AppSecurityPolicy
        {
            ToolResultEnabled = entity.Enabled && entity.MaskToolResult,
            ToolArgsEnabled = entity.Enabled && entity.MaskToolArgs,
            ModelOutputEnabled = entity.MaskModelOutput,
        };

        // 内容规则仅在总开关启用时生效；模型回复规则独立门控，不受总开关影响
        policy.ContentMaskActive = entity.Enabled && TryCompile(entity.Rules, policy._contentRules);
        policy.ModelMaskActive = policy.ModelOutputEnabled && TryCompile(entity.ModelOutputRules, policy._modelRules);

        return policy.IsActive ? policy : Disabled;
    }

    /// <summary>
    /// 编译规则 JSON 到目标列表（损坏内容与不可编译规则静默跳过）；至少编译出一条返回 true.
    /// </summary>
    private static bool TryCompile(string? rulesJson, List<(Regex Regex, string Replacement)> target)
    {
        if (string.IsNullOrWhiteSpace(rulesJson))
        {
            return false;
        }

        List<AppSecurityRule>? rules;
        try
        {
            rules = JsonSerializer.Deserialize<List<AppSecurityRule>>(rulesJson, RuleJsonOptions);
        }
        catch (JsonException)
        {
            return false;
        }

        if (rules == null)
        {
            return false;
        }

        foreach (var rule in rules)
        {
            var regex = Compile(rule);
            if (regex != null)
            {
                target.Add((regex, string.IsNullOrEmpty(rule.Replacement) ? DefaultReplacement : rule.Replacement));
            }
        }

        return target.Count > 0;
    }

    /// <summary>
    /// 编译单条规则（内置类型按类型取正则，自定义按 pattern）；类型非法或正则不可编译返回 null.
    /// </summary>
    /// <param name="type">规则类型代码.</param>
    /// <param name="pattern">自定义正则（custom 必填，内置类型忽略）.</param>
    /// <returns>编译后的正则，非法返回 null.</returns>
    public static Regex? CompileRule(string? type, string? pattern)
    {
        return Compile(new AppSecurityRule { Type = type, Pattern = pattern });
    }

    private static Regex? Compile(AppSecurityRule rule)
    {
        var pattern = rule.Type != null && BuiltinPatterns.TryGetValue(rule.Type, out var builtin)
            ? builtin
            : rule.Type == AppSecurityRuleTypes.Custom
                ? rule.Pattern
                : null;
        if (string.IsNullOrWhiteSpace(pattern))
        {
            return null;
        }

        try
        {
            return new Regex(pattern, RegexOptions.CultureInvariant, MatchTimeout);
        }
        catch (ArgumentException)
        {
            return null;
        }
    }

    /// <summary>
    /// 应用内容脱敏规则脱敏纯文本；未生效时原样返回.
    /// </summary>
    /// <param name="text">待脱敏文本.</param>
    /// <returns>脱敏后文本.</returns>
    public string MaskText(string? text) => MaskTextCore(_contentRules, text);

    /// <summary>
    /// 工具调用结果（含错误信息、流程节点输出）脱敏：JSON 文本按内容规则做节点级脱敏（字符串与数字字面量均处理），解析失败退化为纯文本脱敏.
    /// </summary>
    /// <param name="text">结果文本.</param>
    /// <returns>脱敏后文本.</returns>
    public string MaskToolResultText(string? text) => ToolResultEnabled ? MaskJsonLikeText(_contentRules, text) : text ?? string.Empty;

    /// <summary>
    /// 工具调用参数（调用记录展示、流程节点输入）脱敏：JSON 文本按内容规则做节点级脱敏，解析失败退化为纯文本脱敏.
    /// </summary>
    /// <param name="text">参数 JSON 文本.</param>
    /// <returns>脱敏后文本.</returns>
    public string MaskToolArgsText(string? text) => ToolArgsEnabled ? MaskJsonLikeText(_contentRules, text) : text ?? string.Empty;

    /// <summary>
    /// 单个工具调用参数值脱敏（纯文本内容规则，不做 JSON 解析；用于参数字典字符串值）.
    /// </summary>
    /// <param name="text">参数值文本.</param>
    /// <returns>脱敏后文本.</returns>
    public string MaskToolArgValue(string? text) => ToolArgsEnabled ? MaskText(text) : text ?? string.Empty;

    /// <summary>
    /// 模型回复文本（对话正文）脱敏：按模型回复专属规则执行.
    /// </summary>
    /// <param name="text">回复文本.</param>
    /// <returns>脱敏后文本.</returns>
    public string MaskModelText(string? text) => ModelOutputEnabled ? MaskTextCore(_modelRules, text) : text ?? string.Empty;

    /// <summary>
    /// 按内容规则就地脱敏 JSON 对象树中所有字符串与数字字面量；未生效时不动原对象.
    /// </summary>
    /// <param name="node">JSON 节点（Object/Array/Value），可为 null.</param>
    public void MaskJson(JsonNode? node) => MaskJsonCore(_contentRules, node);

    /// <summary>
    /// 按「工具调用参数」范围就地脱敏 JSON 对象树（流程节点输入）；范围未开启时不动原对象.
    /// </summary>
    /// <param name="node">JSON 节点，可为 null.</param>
    public void MaskToolArgsJson(JsonNode? node)
    {
        if (ToolArgsEnabled)
        {
            MaskJson(node);
        }
    }

    /// <summary>
    /// 按「工具调用结果」范围就地脱敏 JSON 对象树（流程节点输出）；范围未开启时不动原对象.
    /// </summary>
    /// <param name="node">JSON 节点，可为 null.</param>
    public void MaskToolResultJson(JsonNode? node)
    {
        if (ToolResultEnabled)
        {
            MaskJson(node);
        }
    }

    /// <summary>
    /// 按模型回复专属规则就地脱敏 JSON 对象树（AI 对话/Agent 应用节点的模型输出）；未生效时不动原对象.
    /// </summary>
    /// <param name="node">JSON 节点，可为 null.</param>
    public void MaskModelJson(JsonNode? node)
    {
        if (ModelOutputEnabled)
        {
            MaskJsonCore(_modelRules, node);
        }
    }

    private static string MaskTextCore(List<(Regex Regex, string Replacement)> rules, string? text)
    {
        if (rules.Count == 0 || string.IsNullOrEmpty(text))
        {
            return text ?? string.Empty;
        }

        foreach (var (regex, replacement) in rules)
        {
            try
            {
                text = regex.Replace(text, replacement);
            }
            catch (RegexMatchTimeoutException)
            {
                // 单条规则超时跳过，继续其余规则，避免灾难性回溯阻断对话
            }
        }

        return text;
    }

    private static void MaskJsonCore(List<(Regex Regex, string Replacement)> rules, JsonNode? node)
    {
        if (rules.Count == 0 || node is null)
        {
            return;
        }

        MaskJsonNode(rules, node);
    }

    private static void MaskJsonNode(List<(Regex Regex, string Replacement)> rules, JsonNode node)
    {
        switch (node)
        {
            case JsonObject obj:
                foreach (var key in obj.Select(static kv => kv.Key).ToList())
                {
                    var value = obj[key];
                    if (value is JsonValue)
                    {
                        obj[key] = MaskJsonValue(rules, value);
                    }
                    else
                    {
                        MaskJsonCore(rules, value);
                    }
                }

                break;
            case JsonArray array:
                for (var i = 0; i < array.Count; i++)
                {
                    var value = array[i];
                    if (value is JsonValue)
                    {
                        array[i] = MaskJsonValue(rules, value);
                    }
                    else
                    {
                        MaskJsonCore(rules, value);
                    }
                }

                break;
        }
    }

    private static JsonNode? MaskJsonValue(List<(Regex Regex, string Replacement)> rules, JsonNode value)
    {
        if (value is not JsonValue jsonValue)
        {
            return value;
        }

        if (jsonValue.TryGetValue(out string? text))
        {
            var masked = MaskTextCore(rules, text);
            return masked == text ? value : JsonValue.Create(masked);
        }

        // 数字字面量（电话号/卡号等以 number 形态出现的场景）：转文本脱敏，命中后降级为字符串节点
        if (jsonValue.TryGetValue(out long l))
        {
            var raw = l.ToString(CultureInfo.InvariantCulture);
            var masked = MaskTextCore(rules, raw);
            return masked == raw ? value : JsonValue.Create(masked);
        }

        if (jsonValue.TryGetValue(out double d))
        {
            var raw = d.ToString("R", CultureInfo.InvariantCulture);
            var masked = MaskTextCore(rules, raw);
            return masked == raw ? value : JsonValue.Create(masked);
        }

        return value;
    }

    private static string MaskJsonLikeText(List<(Regex Regex, string Replacement)> rules, string? text)
    {
        if (rules.Count == 0 || string.IsNullOrEmpty(text))
        {
            return text ?? string.Empty;
        }

        JsonNode? node;
        try
        {
            node = JsonNode.Parse(text);
        }
        catch (JsonException)
        {
            return MaskTextCore(rules, text);
        }

        if (node is null)
        {
            return text;
        }

        MaskJsonNode(rules, node);
        return node.ToJsonString(RuleJsonOptions);
    }

    /// <summary>
    /// 脱敏规则存储/传输契约（app_security_config.rules 数组元素）.
    /// </summary>
    public sealed class AppSecurityRule
    {
        /// <summary>
        /// 规则名称（展示用）.
        /// </summary>
        public string? Name { get; set; }

        /// <summary>
        /// 规则类型代码，见 <see cref="AppSecurityRuleTypes"/>.
        /// </summary>
        public string? Type { get; set; }

        /// <summary>
        /// 自定义正则（type=custom 时必填，内置类型忽略）.
        /// </summary>
        public string? Pattern { get; set; }

        /// <summary>
        /// 命中后替换文本，空为 ***.
        /// </summary>
        public string? Replacement { get; set; }
    }
}
