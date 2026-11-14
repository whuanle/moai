using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace MoAI.AIPlugin.Models;

/// <summary>
/// 插件工具调用策略（按工具粒度的语义限制）：denyPatterns 命中参数 JSON 时按 action 处置.
/// 注意：这是语义引导与纵深防御，不构成安全边界——真正的强制仍依赖执行层（如只读数据库账号）.
/// </summary>
public class PluginToolCallPolicy
{
    /// <summary>
    /// action：命中限制规则后拦截调用并向模型返回拒绝信息.
    /// </summary>
    public const string ActionBlock = "block";

    /// <summary>
    /// action：命中后转人工确认（审批模式复用工具审批链；自动模式下等价 block）.
    /// </summary>
    public const string ActionConfirm = "confirm";

    /// <summary>
    /// action：仅标记不拦截（当前实现下等价放行）.
    /// </summary>
    public const string ActionAudit = "audit";

    /// <summary>
    /// 合法的 action 取值.
    /// </summary>
    public static readonly string[] AllowedActions = [ActionBlock, ActionConfirm, ActionAudit];

    private static readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web);

    private readonly List<Regex>? _compiled;

    /// <summary>
    /// Initializes a new instance of the <see cref="PluginToolCallPolicy"/> class.
    /// </summary>
    /// <param name="denyPatterns">拒绝规则正则列表（对工具参数 JSON 全文匹配，忽略大小写）.</param>
    /// <param name="action">处置动作（block/confirm/audit）.</param>
    public PluginToolCallPolicy(List<string>? denyPatterns = null, string action = ActionAudit)
    {
        DenyPatterns = denyPatterns ?? [];
        Action = action;
        _compiled = Compile(DenyPatterns);
    }

    /// <summary>
    /// 拒绝规则正则列表（对工具参数 JSON 全文匹配，忽略大小写）.
    /// </summary>
    [JsonPropertyName("denyPatterns")]
    public List<string> DenyPatterns { get; }

    /// <summary>
    /// 处置动作：block/confirm/audit.
    /// </summary>
    [JsonPropertyName("action")]
    public string Action { get; }

    /// <summary>
    /// 策略是否有效（存在拒绝规则）.
    /// </summary>
    [JsonIgnore]
    public bool IsActive => DenyPatterns.Count > 0;

    /// <summary>
    /// 从 JSON 反序列化；空/非法 JSON 返回 null.
    /// </summary>
    /// <param name="json">策略 JSON.</param>
    /// <returns>返回 <see cref="PluginToolCallPolicy"/>，无效返回 null.</returns>
    public static PluginToolCallPolicy? Parse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

#pragma warning disable CA1031 // 运行时坏配置不应中断调用链，返回 null 放行
        try
        {
            var dto = JsonSerializer.Deserialize<PluginToolCallPolicy>(json, _jsonOptions);
            if (dto == null)
            {
                return null;
            }

            return new PluginToolCallPolicy(dto.DenyPatterns, dto.Action);
        }
        catch (Exception)
        {
            return null;
        }
#pragma warning restore CA1031
    }

    /// <summary>
    /// 保存前校验策略 JSON（格式、action 枚举、正则可编译、长度上限）.
    /// </summary>
    /// <param name="json">策略 JSON，空串表示未配置.</param>
    /// <param name="error">校验失败原因.</param>
    /// <returns>返回 true 表示校验通过.</returns>
    public static bool TryValidate(string? json, out string? error)
    {
        error = null;
        if (string.IsNullOrWhiteSpace(json))
        {
            return true;
        }

        PluginToolCallPolicy? dto;
        try
        {
            dto = JsonSerializer.Deserialize<PluginToolCallPolicy>(json, _jsonOptions);
        }
        catch (JsonException ex)
        {
            error = $"调用策略 JSON 不合法：{ex.Message}";
            return false;
        }

        if (dto == null)
        {
            return true;
        }

        if (!AllowedActions.Contains(dto.Action, StringComparer.OrdinalIgnoreCase))
        {
            error = $"调用策略 action 非法：{dto.Action}（允许：{string.Join("/", AllowedActions)}）";
            return false;
        }

        if (dto.DenyPatterns.Count > 20)
        {
            error = "调用策略 denyPatterns 数量不能超过 20";
            return false;
        }

        foreach (var pattern in dto.DenyPatterns)
        {
            if (string.IsNullOrWhiteSpace(pattern))
            {
                error = "调用策略 denyPatterns 不能包含空规则";
                return false;
            }

            if (pattern.Length > 200)
            {
                error = "调用策略单条 denyPattern 长度不能超过 200";
                return false;
            }

            if (Compile([pattern]) == null)
            {
                error = $"调用策略正则不合法：{pattern}";
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// 判定工具调用参数是否命中拒绝规则.
    /// </summary>
    /// <param name="argumentsJson">工具参数 JSON 文本.</param>
    /// <param name="matchedPattern">命中的规则.</param>
    /// <returns>返回 true 表示命中.</returns>
    public bool IsDenied(string argumentsJson, out string? matchedPattern)
    {
        matchedPattern = null;
        if (!IsActive || _compiled == null || string.IsNullOrEmpty(argumentsJson))
        {
            return false;
        }

        foreach (var regex in _compiled)
        {
            if (regex.IsMatch(argumentsJson))
            {
                matchedPattern = regex.ToString();
                return true;
            }
        }

        return false;
    }

    private static List<Regex>? Compile(List<string> patterns)
    {
        var compiled = new List<Regex>(patterns.Count);
        foreach (var pattern in patterns)
        {
#pragma warning disable CA1031 // 坏正则在运行时返回 null 放行，不应中断调用链
            try
            {
                compiled.Add(new Regex(pattern, RegexOptions.IgnoreCase | RegexOptions.Compiled, TimeSpan.FromMilliseconds(200)));
            }
            catch (Exception)
            {
                return null;
            }
#pragma warning restore CA1031
        }

        return compiled;
    }
}
