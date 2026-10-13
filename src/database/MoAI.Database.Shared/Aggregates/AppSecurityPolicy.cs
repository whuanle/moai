using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
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

    /// <summary>提示词注入启发式（内置中英模式集，大小写不敏感；建议配 block/flag 策略）.</summary>
    public const string Injection = "injection";

    /// <summary>
    /// 全部内置类型代码.
    /// </summary>
    public static readonly string[] Builtin = [Phone, IdCard, Email, BankCard, Injection];

    /// <summary>
    /// 全部合法类型代码.
    /// </summary>
    public static readonly string[] All = [Phone, IdCard, Email, BankCard, Injection, Custom];

    /// <summary>
    /// 校验类型代码是否合法.
    /// </summary>
    /// <param name="type">类型代码.</param>
    /// <returns>合法返回 true.</returns>
    public static bool IsValid(string? type) => type is not null && Array.IndexOf(All, type) >= 0;
}

/// <summary>
/// 规则处理策略代码（app_security_config.rules[].strategy），缺省 redact 兼容存量规则.
/// </summary>
public static class AppSecurityStrategies
{
    /// <summary>替换为占位文本（replacement，默认 ***）.</summary>
    public const string Redact = "redact";

    /// <summary>部分遮蔽（内置类型按类型样式，custom/银行卡保留末 keepLast 位）.</summary>
    public const string Mask = "mask";

    /// <summary>替换为确定性哈希（SHA256 加 pepper 截断，保留可关联性）.</summary>
    public const string Hash = "hash";

    /// <summary>命中即拦截（工具参数预检跳过执行、用户输入拒绝；模型回复作用域不支持）.</summary>
    public const string Block = "block";

    /// <summary>观测模式：内容原样放行，仅记录安全事件.</summary>
    public const string Flag = "flag";

    /// <summary>
    /// 全部合法策略代码.
    /// </summary>
    public static readonly string[] All = [Redact, Mask, Hash, Block, Flag];

    /// <summary>
    /// 校验策略代码是否合法.
    /// </summary>
    /// <param name="strategy">策略代码.</param>
    /// <returns>合法返回 true.</returns>
    public static bool IsValid(string? strategy) => strategy is not null && Array.IndexOf(All, strategy) >= 0;
}

/// <summary>
/// 安全事件作用域代码（app_security_event.scope）.
/// </summary>
public static class AppSecurityScopes
{
    /// <summary>用户输入（发送给模型的用户消息文本）.</summary>
    public const string Input = "input";

    /// <summary>工具调用结果（含错误信息、流程节点输出）.</summary>
    public const string ToolResult = "toolResult";

    /// <summary>工具调用参数（调用记录、流程节点输入）.</summary>
    public const string ToolArgs = "toolArgs";

    /// <summary>模型回复文本（对话正文）.</summary>
    public const string ModelOutput = "modelOutput";

    /// <summary>
    /// 全部作用域代码.
    /// </summary>
    public static readonly string[] All = [Input, ToolResult, ToolArgs, ModelOutput];
}

/// <summary>
/// 安全事件动作代码（app_security_event.action）.
/// </summary>
public static class AppSecurityActions
{
    /// <summary>已脱敏/替换.</summary>
    public const string Masked = "masked";

    /// <summary>已拦截.</summary>
    public const string Blocked = "blocked";

    /// <summary>仅记录未处理（flag 观测模式）.</summary>
    public const string Flagged = "flagged";
}

/// <summary>
/// 命中收集器：一次运行内按（作用域, 规则, 策略, 动作）聚合计数，运行结束后批量写审计.
/// </summary>
public sealed class AppSecurityHitCollector
{
    private readonly Dictionary<(string Scope, string RuleName, string RuleType, string Strategy, string Action), int> _hits = [];

    /// <summary>
    /// 记录一次命中（同键累加）.
    /// </summary>
    /// <param name="scope">作用域代码.</param>
    /// <param name="ruleName">规则名称.</param>
    /// <param name="ruleType">规则类型代码.</param>
    /// <param name="strategy">策略代码.</param>
    /// <param name="action">动作代码.</param>
    /// <param name="count">命中次数，默认 1.</param>
    public void Add(string scope, string ruleName, string ruleType, string strategy, string action, int count = 1)
    {
        var key = (scope, ruleName, ruleType, strategy, action);
        _hits.TryGetValue(key, out var current);
        _hits[key] = current + count;
    }

    /// <summary>
    /// 是否有 block 策略命中.
    /// </summary>
    public bool HasBlocked => _hits.Keys.Any(k => k.Action == AppSecurityActions.Blocked);

    /// <summary>
    /// 取快照（聚合行，键内含作用域/规则/策略/动作与次数）.
    /// </summary>
    /// <returns>聚合命中列表.</returns>
    public IReadOnlyList<(string Scope, string RuleName, string RuleType, string Strategy, string Action, int Count)> Snapshot()
    {
        return _hits.Select(kv => (kv.Key.Scope, kv.Key.RuleName, kv.Key.RuleType, kv.Key.Strategy, kv.Key.Action, kv.Value)).ToList();
    }

    /// <summary>
    /// 清空已收集命中（快照写出后调用）.
    /// </summary>
    public void Reset() => _hits.Clear();
}

/// <summary>
/// 应用内容安全策略（app_security_config 聚合）：规则编译与文本/JSON 处理执行.
    /// <para>两组规则相互独立维护：<see cref="AppSecurityConfigEntity.Rules"/>（内容规则）作用于用户输入、工具调用结果（含错误信息、流程节点输出）与工具调用参数（调用记录展示、流程节点输入）；
    /// <see cref="AppSecurityConfigEntity.ModelOutputRules"/>（模型规则）仅作用于模型回复文本，且不依赖内容脱敏总开关.</para>
    /// <para>每条规则带处理策略（redact/mask/hash/block/flag）：读侧兜底等非强制上下文中 block 降级为占位替换、flag 原样放行；
    /// 强制上下文（Agent 中间件）block 命中会拒绝执行/拒绝输入.</para>
/// <para>未启用 / 无规则 / 对应范围未开启时，各处理方法原样返回输入.</para>
/// </summary>
public sealed class AppSecurityPolicy
{
    /// <summary>
    /// 默认替换文本.
    /// </summary>
    public const string DefaultReplacement = "***";

    /// <summary>
    /// block 命中后的占位文本（非强制上下文降级替换用）.
    /// </summary>
    public const string BlockPlaceholder = "[BLOCKED]";

    /// <summary>
    /// 最多规则条数（保存入口校验）.
    /// </summary>
    public const int MaxRuleCount = 50;

    /// <summary>
    /// hash 策略输出长度（hex 字符数，64bit）.
    /// </summary>
    public const int HashLength = 16;

    /// <summary>
    /// mask 策略 custom/银行卡默认保留末位.
    /// </summary>
    public const int DefaultKeepLast = 4;

    private static readonly TimeSpan MatchTimeout = TimeSpan.FromMilliseconds(500);

    private static readonly Dictionary<string, string> BuiltinPatterns = new()
    {
        [AppSecurityRuleTypes.Phone] = @"(?<!\d)1[3-9]\d{9}(?!\d)",
        [AppSecurityRuleTypes.IdCard] = @"(?<!\d)\d{17}[\dXx](?!\d)",
        [AppSecurityRuleTypes.Email] = @"[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\.[A-Za-z]{2,}",
        [AppSecurityRuleTypes.BankCard] = @"(?<!\d)\d{16,19}(?!\d)",
        // 提示词注入启发式：低误报强信号模式（指令覆盖/系统提示窃取/越狱关键词），大小写不敏感；中英文语序均覆盖
        [AppSecurityRuleTypes.Injection] = @"(?i)(ignore\s+(all\s+)?(previous|prior|above|earlier)\s+(instructions?|prompts?|rules?|messages?)|忽略(之前|以上|上面|先前|前面)的?(指令|提示|规则|设定|内容)|忽略(之前|以上|上面|先前|前面)(所有|全部)?(指令|提示|规则)|disregard\s+(all\s+)?(previous|prior|above)\s+(instructions?|prompts?|rules?)|(reveal|disclose|print|show|repeat|输出|打印|泄露|透露|复述)[^.\n]{0,12}(system\s+prompt|system\s+instructions?|系统提示(词|语)?|系统指令)|(system\s+prompt|system\s+instructions?|系统提示(词|语)?|系统指令)[^.\n]{0,12}(输出|打印|泄露|透露|复述|显示|给我|发我)|(developer|dan)\s+mode|jailbreak|越狱模式|绕过(内容)?(审查|限制|过滤))",
    };

    private static readonly JsonSerializerOptions RuleJsonOptions = new(JsonSerializerDefaults.Web);

    /// <summary>
    /// 未启用脱敏的空策略（各处理方法为恒等函数）.
    /// </summary>
    public static readonly AppSecurityPolicy Disabled = new();

    private readonly List<CompiledRule> _contentRules = [];

    private readonly List<CompiledRule> _modelRules = [];

    private AppSecurityPolicy()
    {
    }

    /// <summary>
    /// 策略所属应用 id（未启用为 <see cref="Guid.Empty"/>）.
    /// </summary>
    public Guid AppId { get; private set; }

    /// <summary>
    /// 策略所属团队 id（未启用为 0）.
    /// </summary>
    public int TeamId { get; private set; }

    /// <summary>
    /// 是否任意一组规则生效（内容规则或模型回复规则）.
    /// </summary>
    public bool IsActive => ContentMaskActive || ModelMaskActive;

    /// <summary>
    /// 内容规则是否生效（总开关启用且存在至少一条可编译内容规则）.
    /// </summary>
    public bool ContentMaskActive { get; private set; }

    /// <summary>
    /// 模型回复规则是否生效（模型回复范围开启且存在至少一条可编译模型规则；与内容脱敏总开关无关）.
    /// </summary>
    public bool ModelMaskActive { get; private set; }

    /// <summary>
    /// 是否处理工具调用结果（含错误信息、流程节点输出）（含内容脱敏总开关门控）.
    /// </summary>
    public bool ToolResultEnabled { get; private set; }

    /// <summary>
    /// 是否处理工具调用参数（调用记录展示、流程节点输入）（含内容脱敏总开关门控）.
    /// </summary>
    public bool ToolArgsEnabled { get; private set; }

    /// <summary>
    /// 是否处理模型回复文本（对话正文）.
    /// </summary>
    public bool ModelOutputEnabled { get; private set; }

    /// <summary>
    /// 模型回复规则是否含 block 策略（流式缓冲窗拦截的启用条件）.
    /// </summary>
    public bool ModelBlockActive { get; private set; }

    /// <summary>
    /// 是否处理用户输入文本（含内容脱敏总开关门控）.
    /// </summary>
    public bool InputEnabled { get; private set; }

    /// <summary>
    /// hash 策略的加盐文本（部署级派生，重启稳定）.
    /// </summary>
    private string HashPepper { get; set; } = string.Empty;

    /// <summary>
    /// 从配置实体解析策略；实体为 null 或两组规则均未生效时返回 <see cref="Disabled"/>.
    /// </summary>
    /// <param name="entity">安全配置实体（可为 null）.</param>
    /// <param name="hashPepper">hash 策略加盐文本（部署级派生，空串=未派生，hash 规则降级为 redact）.</param>
    /// <returns>安全策略.</returns>
    public static AppSecurityPolicy Parse(AppSecurityConfigEntity? entity, string hashPepper = "")
    {
        if (entity == null)
        {
            return Disabled;
        }

        var policy = new AppSecurityPolicy
        {
            AppId = entity.AppId,
            TeamId = entity.TeamId,
            HashPepper = hashPepper ?? string.Empty,
            ToolResultEnabled = entity.Enabled && entity.MaskToolResult,
            ToolArgsEnabled = entity.Enabled && entity.MaskToolArgs,
            ModelOutputEnabled = entity.MaskModelOutput,
            InputEnabled = entity.Enabled && entity.MaskInput,
        };

        // 内容规则仅在总开关启用时生效；模型回复规则独立门控，不受总开关影响
        policy.ContentMaskActive = entity.Enabled && TryCompile(entity.Rules, hashPepper, policy._contentRules);
        policy.ModelMaskActive = policy.ModelOutputEnabled && TryCompile(entity.ModelOutputRules, hashPepper, policy._modelRules);
        policy.ModelBlockActive = policy.ModelMaskActive && policy._modelRules.Any(x => x.Strategy == AppSecurityStrategies.Block);

        return policy.IsActive ? policy : Disabled;
    }

    /// <summary>
    /// 编译规则 JSON 到目标列表（损坏内容与不可编译规则静默跳过）；至少编译出一条返回 true.
    /// </summary>
    private static bool TryCompile(string? rulesJson, string hashPepper, List<CompiledRule> target)
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
            var compiled = Compile(rule, hashPepper);
            if (compiled != null)
            {
                target.Add(compiled);
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
        return Compile(new AppSecurityRule { Type = type, Pattern = pattern }, string.Empty)?.Regex;
    }

    private static CompiledRule? Compile(AppSecurityRule rule, string hashPepper)
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

        Regex regex;
        try
        {
            regex = new Regex(pattern, RegexOptions.CultureInvariant, MatchTimeout);
        }
        catch (ArgumentException)
        {
            return null;
        }

        // 存量规则无 strategy 字段时反序列化为 null，归一为 redact
        var strategy = string.IsNullOrWhiteSpace(rule.Strategy) ? AppSecurityStrategies.Redact : rule.Strategy!;
        if (strategy == AppSecurityStrategies.Hash && string.IsNullOrEmpty(hashPepper))
        {
            // 未派生 pepper 时 hash 不可逆推不可关联，降级为 redact 保证不裸露原文
            strategy = AppSecurityStrategies.Redact;
        }

        var keepLast = rule.KeepLast ?? DefaultKeepLast;
        keepLast = Math.Clamp(keepLast, 0, 18);

        return new CompiledRule(
            regex,
            rule.Type ?? AppSecurityRuleTypes.Custom,
            rule.Name ?? string.Empty,
            strategy,
            string.IsNullOrEmpty(rule.Replacement) ? DefaultReplacement : rule.Replacement!,
            keepLast,
            hashPepper);
    }

    /// <summary>
    /// 应用内容规则处理用户输入文本；未生效时原样返回.
    /// </summary>
    /// <param name="text">输入文本.</param>
    /// <returns>处理后文本.</returns>
    public string MaskInputText(string? text) => InputEnabled ? MaskTextCore(_contentRules, AppSecurityScopes.Input, text, null) : text ?? string.Empty;

    /// <summary>
    /// 应用内容规则脱敏纯文本；未生效时原样返回.
    /// </summary>
    /// <param name="text">待脱敏文本.</param>
    /// <returns>脱敏后文本.</returns>
    public string MaskText(string? text) => MaskTextCore(_contentRules, AppSecurityScopes.ToolResult, text, null);

    /// <summary>
    /// 工具调用结果（含错误信息、流程节点输出）脱敏：JSON 文本按内容规则做节点级脱敏（字符串与数字字面量均处理），解析失败退化为纯文本脱敏.
    /// </summary>
    /// <param name="text">结果文本.</param>
    /// <returns>脱敏后文本.</returns>
    public string MaskToolResultText(string? text) => ToolResultEnabled ? MaskJsonLikeText(_contentRules, AppSecurityScopes.ToolResult, text) : text ?? string.Empty;

    /// <summary>
    /// 工具调用参数（调用记录展示、流程节点输入）脱敏：JSON 文本按内容规则做节点级脱敏，解析失败退化为纯文本脱敏.
    /// </summary>
    /// <param name="text">参数 JSON 文本.</param>
    /// <returns>脱敏后文本.</returns>
    public string MaskToolArgsText(string? text) => ToolArgsEnabled ? MaskJsonLikeText(_contentRules, AppSecurityScopes.ToolArgs, text) : text ?? string.Empty;

    /// <summary>
    /// 单个工具调用参数值脱敏（纯文本内容规则，不做 JSON 解析；用于参数字典字符串值）.
    /// </summary>
    /// <param name="text">参数值文本.</param>
    /// <returns>脱敏后文本.</returns>
    public string MaskToolArgValue(string? text) => ToolArgsEnabled ? MaskTextCore(_contentRules, AppSecurityScopes.ToolArgs, text, null) : text ?? string.Empty;

    /// <summary>
    /// 单个工具调用参数值脱敏并记录命中（Agent 中间件强制路径使用）.
    /// </summary>
    /// <param name="text">参数值文本.</param>
    /// <param name="collector">命中收集器.</param>
    /// <returns>脱敏后文本.</returns>
    public string MaskToolArgValue(string? text, AppSecurityHitCollector collector) => ToolArgsEnabled ? MaskTextCore(_contentRules, AppSecurityScopes.ToolArgs, text, collector) : text ?? string.Empty;

    /// <summary>
    /// 模型回复文本（对话正文）脱敏：按模型回复专属规则执行（block 策略降级占位替换）.
    /// </summary>
    /// <param name="text">回复文本.</param>
    /// <returns>脱敏后文本.</returns>
    public string MaskModelText(string? text) => ModelOutputEnabled ? MaskTextCore(_modelRules, AppSecurityScopes.ModelOutput, text, null) : text ?? string.Empty;

    /// <summary>
    /// 模型回复文本（对话正文）脱敏并记录命中（Agent 中间件强制路径使用）.
    /// </summary>
    /// <param name="text">回复文本.</param>
    /// <param name="collector">命中收集器.</param>
    /// <returns>脱敏后文本.</returns>
    public string MaskModelText(string? text, AppSecurityHitCollector collector) => ModelOutputEnabled ? MaskTextCore(_modelRules, AppSecurityScopes.ModelOutput, text, collector) : text ?? string.Empty;

    /// <summary>
    /// 模型回复文本脱敏但跳过 block 策略（流式缓冲路径：block 检测交给缓冲窗整段判定，避免 span 级占位抢先替换）.
    /// </summary>
    /// <param name="text">回复文本.</param>
    /// <param name="collector">命中收集器.</param>
    /// <returns>脱敏后文本（不含 block 处理）.</returns>
    public string MaskModelTextExcludeBlock(string? text, AppSecurityHitCollector collector) => ModelOutputEnabled ? MaskTextCore(_modelRules, AppSecurityScopes.ModelOutput, text, collector, excludeBlock: true) : text ?? string.Empty;

    /// <summary>
    /// 模型回复文本 block 检测（仅扫描 block 策略规则，命中记录审计）.
    /// </summary>
    /// <param name="text">待检测文本.</param>
    /// <param name="collector">命中收集器.</param>
    /// <returns>命中返回 true.</returns>
    public bool IsModelTextBlocked(string? text, AppSecurityHitCollector collector)
    {
        if (!ModelBlockActive || string.IsNullOrEmpty(text))
        {
            return false;
        }

        foreach (var rule in _modelRules)
        {
            if (rule.Strategy != AppSecurityStrategies.Block)
            {
                continue;
            }

            try
            {
                if (rule.Regex.IsMatch(text))
                {
                    collector.Add(AppSecurityScopes.ModelOutput, rule.Name, rule.Type, rule.Strategy, AppSecurityActions.Blocked);
                    return true;
                }
            }
            catch (RegexMatchTimeoutException)
            {
                // 超时视为未命中，不阻断对话
            }
        }

        return false;
    }

    /// <summary>
    /// 强制执行模型回复规则（非流式整条路径）：先按非 block 策略脱敏，再对结果做 block 检测，命中整段替换为拦截占位.
    /// </summary>
    /// <param name="text">回复文本.</param>
    /// <param name="collector">命中收集器.</param>
    /// <returns>处理结果.</returns>
    public AppSecurityTextOutcome EnforceModelText(string? text, AppSecurityHitCollector collector)
    {
        if (!ModelOutputEnabled || string.IsNullOrEmpty(text))
        {
            return new AppSecurityTextOutcome { Text = text ?? string.Empty };
        }

        var masked = MaskTextCore(_modelRules, AppSecurityScopes.ModelOutput, text, collector, excludeBlock: true);
        if (IsModelTextBlocked(masked, collector))
        {
            return new AppSecurityTextOutcome { Text = BlockPlaceholder, Blocked = true };
        }

        return new AppSecurityTextOutcome { Text = masked };
    }

    /// <summary>
    /// 强制执行内容规则于用户输入：block 命中返回 <c>Blocked=true</c>；flag 原样放行；命中写入收集器.
    /// </summary>
    /// <param name="text">输入文本.</param>
    /// <param name="collector">命中收集器.</param>
    /// <returns>处理结果.</returns>
    public AppSecurityTextOutcome EnforceInputText(string? text, AppSecurityHitCollector collector)
        => InputEnabled ? EnforceCore(_contentRules, AppSecurityScopes.Input, text, collector) : new AppSecurityTextOutcome { Text = text ?? string.Empty };

    /// <summary>
    /// 强制执行内容规则于工具调用结果：block 命中整段替换为拦截占位；命中写入收集器.
    /// </summary>
    /// <param name="text">结果文本.</param>
    /// <param name="collector">命中收集器.</param>
    /// <returns>处理结果.</returns>
    public AppSecurityTextOutcome EnforceToolResultText(string? text, AppSecurityHitCollector collector)
        => ToolResultEnabled ? EnforceJsonLikeText(_contentRules, AppSecurityScopes.ToolResult, text, collector) : new AppSecurityTextOutcome { Text = text ?? string.Empty };

    /// <summary>
    /// 工具调用参数拦截预检（执行前调用）：仅扫描 block 策略规则，命中返回 true（命中写入收集器）.
    /// </summary>
    /// <param name="arguments">参数字典.</param>
    /// <param name="collector">命中收集器.</param>
    /// <returns>命中返回 true，应拒绝本次调用.</returns>
    public bool IsToolCallBlocked(IDictionary<string, object?>? arguments, AppSecurityHitCollector collector)
    {
        if (!ToolArgsEnabled || arguments == null || arguments.Count == 0)
        {
            return false;
        }

        var blockRules = _contentRules.Where(x => x.Strategy == AppSecurityStrategies.Block).ToList();
        if (blockRules.Count == 0)
        {
            return false;
        }

        foreach (var (key, value) in arguments)
        {
            if (value is not string text || string.IsNullOrEmpty(text))
            {
                continue;
            }

            foreach (var rule in blockRules)
            {
                if (rule.Regex.IsMatch(text))
                {
                    collector.Add(AppSecurityScopes.ToolArgs, rule.Name, rule.Type, rule.Strategy, AppSecurityActions.Blocked);
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>
    /// 按内容规则就地处理 JSON 对象树中所有字符串与数字字面量；未生效时不动原对象.
    /// </summary>
    /// <param name="node">JSON 节点（Object/Array/Value），可为 null.</param>
    public void MaskJson(JsonNode? node) => MaskJsonCore(_contentRules, AppSecurityScopes.ToolResult, node);

    /// <summary>
    /// 按「工具调用参数」范围就地处理 JSON 对象树（流程节点输入）；范围未开启时不动原对象.
    /// </summary>
    /// <param name="node">JSON 节点，可为 null.</param>
    public void MaskToolArgsJson(JsonNode? node)
    {
        if (ToolArgsEnabled)
        {
            MaskJsonCore(_contentRules, AppSecurityScopes.ToolArgs, node);
        }
    }

    /// <summary>
    /// 按「工具调用结果」范围就地处理 JSON 对象树（流程节点输出）；范围未开启时不动原对象.
    /// </summary>
    /// <param name="node">JSON 节点，可为 null.</param>
    public void MaskToolResultJson(JsonNode? node)
    {
        if (ToolResultEnabled)
        {
            MaskJsonCore(_contentRules, AppSecurityScopes.ToolResult, node);
        }
    }

    /// <summary>
    /// 按模型回复专属规则就地处理 JSON 对象树（AI 对话/Agent 应用节点的模型输出）；未生效时不动原对象.
    /// </summary>
    /// <param name="node">JSON 节点，可为 null.</param>
    public void MaskModelJson(JsonNode? node)
    {
        if (ModelOutputEnabled)
        {
            MaskJsonCore(_modelRules, AppSecurityScopes.ModelOutput, node);
        }
    }

    private static string MaskTextCore(List<CompiledRule> rules, string scope, string? text, AppSecurityHitCollector? collector, bool excludeBlock = false)
    {
        if (rules.Count == 0 || string.IsNullOrEmpty(text))
        {
            return text ?? string.Empty;
        }

        foreach (var rule in rules)
        {
            if (excludeBlock && rule.Strategy == AppSecurityStrategies.Block)
            {
                continue;
            }

            try
            {
                text = rule.Regex.Replace(text, match => ApplyRule(rule, scope, match, collector));
            }
            catch (RegexMatchTimeoutException)
            {
                // 单条规则超时跳过，继续其余规则，避免灾难性回溯阻断对话
            }
        }

        return text;
    }

    /// <summary>
    /// 强制执行：block 命中整段替换为 <see cref="BlockPlaceholder"/> 并标记 Blocked；其余策略逐命中替换.
    /// </summary>
    private static AppSecurityTextOutcome EnforceCore(List<CompiledRule> rules, string scope, string? text, AppSecurityHitCollector collector)
    {
        if (rules.Count == 0 || string.IsNullOrEmpty(text))
        {
            return new AppSecurityTextOutcome { Text = text ?? string.Empty };
        }

        foreach (var rule in rules)
        {
            if (rule.Strategy != AppSecurityStrategies.Block)
            {
                continue;
            }

            try
            {
                if (rule.Regex.IsMatch(text))
                {
                    collector.Add(scope, rule.Name, rule.Type, rule.Strategy, AppSecurityActions.Blocked);
                    return new AppSecurityTextOutcome { Text = BlockPlaceholder, Blocked = true };
                }
            }
            catch (RegexMatchTimeoutException)
            {
                // 超时视为未命中，不阻断对话
            }
        }

        return new AppSecurityTextOutcome { Text = MaskTextCore(rules, scope, text, collector, excludeBlock: true) };
    }

    private static AppSecurityTextOutcome EnforceJsonLikeText(List<CompiledRule> rules, string scope, string? text, AppSecurityHitCollector collector)
    {
        if (rules.Count == 0 || string.IsNullOrEmpty(text))
        {
            return new AppSecurityTextOutcome { Text = text ?? string.Empty };
        }

        JsonNode? node;
        try
        {
            node = JsonNode.Parse(text);
        }
        catch (JsonException)
        {
            return EnforceCore(rules, scope, text, collector);
        }

        if (node is null)
        {
            return new AppSecurityTextOutcome { Text = text };
        }

        if (ApplyJsonNode(rules, scope, node, collector, enforcing: true))
        {
            return new AppSecurityTextOutcome { Text = BlockPlaceholder, Blocked = true };
        }

        return new AppSecurityTextOutcome { Text = node.ToJsonString(RuleJsonOptions) };
    }

    private static string ApplyRule(CompiledRule rule, string scope, Match match, AppSecurityHitCollector? collector)
    {
        var value = match.Value;
        switch (rule.Strategy)
        {
            case AppSecurityStrategies.Redact:
                collector?.Add(scope, rule.Name, rule.Type, rule.Strategy, AppSecurityActions.Masked);
                return rule.Replacement;
            case AppSecurityStrategies.Mask:
                collector?.Add(scope, rule.Name, rule.Type, rule.Strategy, AppSecurityActions.Masked);
                return MaskValue(rule, value);
            case AppSecurityStrategies.Hash:
                collector?.Add(scope, rule.Name, rule.Type, rule.Strategy, AppSecurityActions.Masked);
                return HashValue(rule, value);
            case AppSecurityStrategies.Block:
                // 非强制上下文降级：占位替换不中断文本
                collector?.Add(scope, rule.Name, rule.Type, rule.Strategy, AppSecurityActions.Blocked);
                return BlockPlaceholder;
            default:
                // flag：原样放行仅记录
                collector?.Add(scope, rule.Name, rule.Type, AppSecurityStrategies.Flag, AppSecurityActions.Flagged);
                return value;
        }
    }

    /// <summary>
    /// mask 策略：内置类型按类型样式遮蔽，custom/银行卡保留末 keepLast 位.
    /// </summary>
    private static string MaskValue(CompiledRule rule, string value)
    {
        switch (rule.Type)
        {
            case AppSecurityRuleTypes.Phone when value.Length >= 7:
                return value[..3] + "****" + value[^4..];
            case AppSecurityRuleTypes.IdCard when value.Length >= 10:
                return value[..6] + new string('*', value.Length - 10) + value[^4..];
            case AppSecurityRuleTypes.Email:
            {
                var at = value.IndexOf('@');
                return at > 0 ? value[0] + "***" + value[at..] : "***";
            }
            default:
            {
                var keep = Math.Min(rule.KeepLast, value.Length);
                return new string('*', value.Length - keep) + value[^keep..];
            }
        }
    }

    /// <summary>
    /// hash 策略：SHA256(pepper + 原值) hex 截断，同值同哈希保留可关联性.
    /// </summary>
    private static string HashValue(CompiledRule rule, string value)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(rule.HashPepper + ":" + value));
        return Convert.ToHexString(bytes).ToLowerInvariant()[..HashLength];
    }

    private static void MaskJsonCore(List<CompiledRule> rules, string scope, JsonNode? node)
    {
        if (rules.Count == 0 || node is null)
        {
            return;
        }

        ApplyJsonNode(rules, scope, node, null, enforcing: false);
    }

    /// <summary>
    /// 就地处理 JSON 节点树；enforcing 时 block 命中返回 true（调用方决定整段替换），普通模式逐值替换.
    /// </summary>
    private static bool ApplyJsonNode(List<CompiledRule> rules, string scope, JsonNode node, AppSecurityHitCollector? collector, bool enforcing)
    {
        switch (node)
        {
            case JsonObject obj:
                foreach (var key in obj.Select(static kv => kv.Key).ToList())
                {
                    var value = obj[key];
                    if (value is JsonValue)
                    {
                        obj[key] = MaskJsonValue(rules, scope, value, collector, enforcing, out var valueBlocked);
                        if (valueBlocked)
                        {
                            return true;
                        }
                    }
                    else if (value != null && ApplyJsonNode(rules, scope, value, collector, enforcing))
                    {
                        return true;
                    }
                }

                break;
            case JsonArray array:
                for (var i = 0; i < array.Count; i++)
                {
                    var value = array[i];
                    if (value is JsonValue)
                    {
                        array[i] = MaskJsonValue(rules, scope, value, collector, enforcing, out var valueBlocked);
                        if (valueBlocked)
                        {
                            return true;
                        }
                    }
                    else if (value != null && ApplyJsonNode(rules, scope, value, collector, enforcing))
                    {
                        return true;
                    }
                }

                break;
        }

        return false;
    }

    private static JsonNode? MaskJsonValue(List<CompiledRule> rules, string scope, JsonNode value, AppSecurityHitCollector? collector, bool enforcing, out bool blocked)
    {
        blocked = false;
        if (value is not JsonValue jsonValue)
        {
            return value;
        }

        if (jsonValue.TryGetValue(out string? text))
        {
            if (enforcing)
            {
                var outcome = EnforceCore(rules, scope, text, collector!);
                blocked = outcome.Blocked;
                return outcome.Text == text ? value : JsonValue.Create(outcome.Text);
            }

            var masked = MaskTextCore(rules, scope, text, collector);
            return masked == text ? value : JsonValue.Create(masked);
        }

        // 数字字面量（电话号/卡号等以 number 形态出现的场景）：转文本处理，命中后降级为字符串节点
        if (jsonValue.TryGetValue(out long l))
        {
            var raw = l.ToString(CultureInfo.InvariantCulture);
            var masked = enforcing ? EnforceCore(rules, scope, raw, collector!) : new AppSecurityTextOutcome { Text = MaskTextCore(rules, scope, raw, collector) };
            blocked = masked.Blocked;
            return masked.Text == raw ? value : JsonValue.Create(masked.Text);
        }

        if (jsonValue.TryGetValue(out double d))
        {
            var raw = d.ToString("R", CultureInfo.InvariantCulture);
            var masked = enforcing ? EnforceCore(rules, scope, raw, collector!) : new AppSecurityTextOutcome { Text = MaskTextCore(rules, scope, raw, collector) };
            blocked = masked.Blocked;
            return masked.Text == raw ? value : JsonValue.Create(masked.Text);
        }

        return value;
    }

    private static string MaskJsonLikeText(List<CompiledRule> rules, string scope, string? text)
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
            return MaskTextCore(rules, scope, text, null);
        }

        if (node is null)
        {
            return text;
        }

        ApplyJsonNode(rules, scope, node, null, enforcing: false);
        return node.ToJsonString(RuleJsonOptions);
    }

    private sealed record CompiledRule(Regex Regex, string Type, string Name, string Strategy, string Replacement, int KeepLast, string HashPepper);

    /// <summary>
    /// 文本处理结果（强制执行模式使用）.
    /// </summary>
    public sealed class AppSecurityTextOutcome
    {
        /// <summary>
        /// 处理后文本.
        /// </summary>
        public string Text { get; init; } = string.Empty;

        /// <summary>
        /// 是否被 block 策略拦截.
        /// </summary>
        public bool Blocked { get; init; }
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
        /// 命中后替换文本（strategy=redact 时生效），空为 ***.
        /// </summary>
        public string? Replacement { get; set; }

        /// <summary>
        /// 处理策略代码，见 <see cref="AppSecurityStrategies"/>；空为 redact（兼容存量规则）.
        /// </summary>
        public string? Strategy { get; set; }

        /// <summary>
        /// mask 策略保留末位（custom/银行卡），0~18；空为 4.
        /// </summary>
        public int? KeepLast { get; set; }
    }
}
