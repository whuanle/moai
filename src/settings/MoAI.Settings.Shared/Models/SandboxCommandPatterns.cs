using System.Collections.Concurrent;
using System.Text.RegularExpressions;

namespace MoAI.Settings.Models;

/// <summary>
/// 沙箱禁用命令模式清单的归一与格式校验（供系统设置保存校验、应用配置校验与运行时命令过滤共用）.
/// <para>模式默认按全词边界匹配（rm → \brm\b，不误杀 confirm），<c>regex:</c> 前缀为原生正则；大小写不敏感；
/// 每条 ≤<see cref="MaxPatternLength"/> 字符、每清单 ≤<see cref="MaxPatterns"/> 条.</para>
/// </summary>
public static class SandboxCommandPatterns
{
    /// <summary>
    /// 单清单模式数上限.
    /// </summary>
    public const int MaxPatterns = 100;

    /// <summary>
    /// 单条模式长度上限.
    /// </summary>
    public const int MaxPatternLength = 200;

    /// <summary>
    /// 原生正则模式前缀.
    /// </summary>
    public const string RegexPrefix = "regex:";

    /// <summary>
    /// 编译缓存（模式 → 编译结果；编译失败缓存 null，由调用方退化处理）.
    /// </summary>
    private static readonly ConcurrentDictionary<string, Regex?> RegexCache = new(StringComparer.Ordinal);

    /// <summary>
    /// 缓存上限：管理员配置的模式总量级有限，超限整体清空重建防止无界增长.
    /// </summary>
    private const int CacheCapacity = 1024;

    /// <summary>
    /// 解析换行分隔的禁用命令清单文本：拆行、去空白、去重（忽略大小写）、截断到上限.
    /// </summary>
    /// <param name="value">清单原文（换行分隔）.</param>
    /// <returns>归一后的模式列表.</returns>
    public static IReadOnlyList<string> ParseList(string? value)
        => Normalize((value ?? string.Empty).Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));

    /// <summary>
    /// 归一模式清单：去空白、去空、去重（忽略大小写）、截断到上限.
    /// </summary>
    /// <param name="patterns">原始清单.</param>
    /// <returns>归一后的清单.</returns>
    public static IReadOnlyList<string> Normalize(IEnumerable<string>? patterns)
    {
        var result = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var raw in patterns ?? [])
        {
            var pattern = raw?.Trim() ?? string.Empty;
            if (pattern.Length == 0 || pattern.Length > MaxPatternLength || !seen.Add(pattern))
            {
                continue;
            }

            result.Add(pattern);
            if (result.Count >= MaxPatterns)
            {
                break;
            }
        }

        return result;
    }

    /// <summary>
    /// 校验单条模式格式：非空、长度合规、regex: 前缀须可编译.
    /// </summary>
    /// <param name="pattern">模式原文.</param>
    /// <returns>合法返回 true.</returns>
    public static bool IsValidPattern(string? pattern)
    {
        if (string.IsNullOrWhiteSpace(pattern) || pattern.Length > MaxPatternLength)
        {
            return false;
        }

        return TryCompile(pattern.Trim(), out _);
    }

    /// <summary>
    /// 取模式对应的编译正则（全词边界或 regex: 原生）；编译失败返回 null.
    /// </summary>
    /// <param name="pattern">归一后的模式.</param>
    /// <returns>编译正则或 null.</returns>
    public static Regex? GetCompiledPattern(string pattern)
    {
        if (RegexCache.Count >= CacheCapacity)
        {
            RegexCache.Clear();
        }

        return RegexCache.GetOrAdd(pattern, static p =>
        {
            TryCompile(p, out var regex);
            return regex;
        });
    }

    private static bool TryCompile(string pattern, out Regex? regex)
    {
        try
        {
            if (pattern.StartsWith(RegexPrefix, StringComparison.OrdinalIgnoreCase))
            {
                var body = pattern[RegexPrefix.Length..].Trim();
                regex = body.Length == 0
                    ? null
                    : new Regex(body, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(500));
                return regex != null;
            }

            regex = new Regex(BuildWordBoundaryPattern(pattern), RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(500));
            return true;
        }
        catch (ArgumentException)
        {
            regex = null;
            return false;
        }
    }

    /// <summary>
    /// 构造全词边界匹配：仅当模式首/尾是词字符时加 \b（\b 在非词字符两侧不成立，如以 ( 开头的字面模式）.
    /// </summary>
    /// <param name="literal">字面模式（已转义前）.</param>
    /// <returns>正则模式串.</returns>
    public static string BuildWordBoundaryPattern(string literal)
    {
        var leading = literal.Length > 0 && IsWordChar(literal[0]) ? @"\b" : string.Empty;
        var trailing = literal.Length > 0 && IsWordChar(literal[^1]) ? @"\b" : string.Empty;
        return $"{leading}{Regex.Escape(literal)}{trailing}";
    }

    private static bool IsWordChar(char c) => char.IsLetterOrDigit(c) || c == '_';
}
