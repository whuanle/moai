using System.Text.RegularExpressions;
using MoAI.Settings.Models;

namespace MoAI.AI.Services;

/// <summary>
/// 沙箱命令过滤判定（纵深防御的一层，非安全边界）：对 sandbox_run_shell 的命令与 sandbox_run_code 的代码文本
/// 按禁用模式清单（平台级 ∪ 应用级）做执行前匹配。匹配语义见 <see cref="SandboxCommandPatterns"/>
/// （默认全词边界、regex: 原生前缀、大小写不敏感）；shell 命令按 bash 操作符粗拆段并逐段与首词（剥 sudo）匹配，
/// 代码文本整段匹配。
/// </summary>
internal static class SandboxCommandGuard
{
    /// <summary>
    /// 判定内容是否命中禁用清单；命中返回块信息（含模式与命中文本），未命中返回 null.
    /// </summary>
    /// <param name="content">命令文本（run_shell.command）或代码文本（run_code.code）.</param>
    /// <param name="isShell">是否 shell 通道（true 拆段+首词匹配，false 整段匹配）.</param>
    /// <param name="patterns">禁用模式清单（平台级 ∪ 应用级，允许未归一）.</param>
    /// <returns>命中信息或 null.</returns>
    public static SandboxCommandBlock? Evaluate(string? content, bool isShell, IReadOnlyList<string> patterns)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return null;
        }

        var normalized = SandboxCommandPatterns.Normalize(patterns);
        if (normalized.Count == 0)
        {
            return null;
        }

        foreach (var subject in CollectSubjects(content, isShell))
        {
            foreach (var pattern in normalized)
            {
                if (Matches(pattern, subject))
                {
                    return new SandboxCommandBlock { Pattern = pattern, MatchedText = subject };
                }
            }
        }

        return null;
    }

    private static IEnumerable<string> CollectSubjects(string content, bool isShell)
    {
        yield return content;
        if (!isShell)
        {
            yield break;
        }

        foreach (var segment in SplitShellSegments(content))
        {
            yield return segment;
            var firstWord = ExtractFirstWord(segment);
            if (firstWord.Length > 0 && !string.Equals(firstWord, segment, StringComparison.Ordinal))
            {
                yield return firstWord;
            }
        }
    }

    /// <summary>
    /// 按 bash 操作符粗拆段（尽力而为：引号内的操作符会被误拆，见文档定位声明）.
    /// </summary>
    private static IEnumerable<string> SplitShellSegments(string command)
    {
        foreach (var raw in command.Split([';', '\n', '|', '&'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (raw.Length > 0)
            {
                yield return raw;
            }
        }
    }

    /// <summary>
    /// 提取首词，剥一层 sudo 前缀（sudo rm x → rm）.
    /// </summary>
    private static string ExtractFirstWord(string segment)
    {
        var text = segment.Trim();
        if (text.StartsWith("sudo ", StringComparison.OrdinalIgnoreCase))
        {
            text = text[5..].TrimStart();
        }

        var space = text.IndexOf(' ');
        return space > 0 ? text[..space] : text;
    }

    private static bool Matches(string pattern, string subject)
    {
        var regex = SandboxCommandPatterns.GetCompiledPattern(pattern);
        if (regex != null)
        {
            return regex.IsMatch(subject);
        }

        // 编译失败（配置层已校验，防御性兜底）：退化为全词字面匹配
        var literal = pattern.StartsWith(SandboxCommandPatterns.RegexPrefix, StringComparison.OrdinalIgnoreCase)
            ? pattern[SandboxCommandPatterns.RegexPrefix.Length..].Trim()
            : pattern;
        return Regex.IsMatch(
            subject,
            SandboxCommandPatterns.BuildWordBoundaryPattern(literal),
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
            TimeSpan.FromMilliseconds(500));
    }
}

/// <summary>
/// 命令过滤命中信息.
/// </summary>
public sealed class SandboxCommandBlock
{
    /// <summary>
    /// 命中的禁用模式原文.
    /// </summary>
    public string Pattern { get; init; } = string.Empty;

    /// <summary>
    /// 命中的命令段 / 首词 / 代码片段.
    /// </summary>
    public string MatchedText { get; init; } = string.Empty;
}
