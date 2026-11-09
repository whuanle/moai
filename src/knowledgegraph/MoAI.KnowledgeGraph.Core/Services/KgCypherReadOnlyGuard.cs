using System;
using System.Collections.Generic;

namespace MoAI.KnowledgeGraph.Services;

/// <summary>
/// Cypher 只读守卫（KG 本地，B7 Text2Cypher 画布模式）：剥除注释、字符串字面量与反引号标识符后，
/// 校验单条语句、只读首关键字白名单与全文写关键字黑名单.
/// </summary>
/// <remarks>
/// 与 aiplugin 的 CypherReadOnlyGuard 同规则集（KG.Core 不能反向引用 AIPlugin，故本地实现）；
/// 纵深防御第二层由 IKgCypherAccessService 承担（$kgId 注入 + 结果侧 kgId 归属校验）.
/// </remarks>
internal static class KgCypherReadOnlyGuard
{
    /// <summary>查询文本最大长度（按剥除前原始文本计）.</summary>
    private const int MaxQueryLength = 8000;

    /// <summary>允许作为语句首关键字的只读前缀.</summary>
    private static readonly HashSet<string> AllowedLeadingKeywords = new(StringComparer.Ordinal)
    {
        "MATCH",
        "OPTIONAL",
        "UNWIND",
        "WITH",
        "RETURN",
    };

    /// <summary>全文禁写的关键字（DML/DDL/过程调用）.</summary>
    private static readonly HashSet<string> ForbiddenKeywords = new(StringComparer.Ordinal)
    {
        "CREATE",
        "MERGE",
        "DELETE",
        "DETACH",
        "SET",
        "REMOVE",
        "LOAD",
        "FOREACH",
        "CALL",
        "DROP",
    };

    /// <summary>
    /// 校验 Cypher 查询文本.
    /// </summary>
    /// <param name="cypher">查询文本.</param>
    /// <returns>返回 null 表示通过，否则返回教学式错误信息.</returns>
    public static string? Validate(string? cypher)
    {
        if (string.IsNullOrWhiteSpace(cypher))
        {
            return "Cypher 不能为空";
        }

        if (cypher.Length > MaxQueryLength)
        {
            return $"Cypher 长度超过上限（{MaxQueryLength} 字符）";
        }

        var stripped = StripLiterals(cypher);

        // 单语句：剥除字面量后不允许分号（尾部分号允许）
        var trimmed = stripped.TrimEnd();
        if (trimmed.EndsWith(';'))
        {
            trimmed = trimmed[..^1].TrimEnd();
        }

        if (trimmed.Contains(';'))
        {
            return "仅允许单条 Cypher 语句";
        }

        // 首关键字白名单
        var first = FirstKeyword(trimmed);
        if (first == null || !AllowedLeadingKeywords.Contains(first))
        {
            return "仅允许只读查询（以 MATCH / OPTIONAL / UNWIND / WITH / RETURN 开头）";
        }

        // 全文写关键字黑名单（按整词匹配）
        foreach (var keyword in ForbiddenKeywords)
        {
            if (ContainsWholeWord(trimmed, keyword))
            {
                return $"只读查询不允许包含 {keyword}";
            }
        }

        return null;
    }

    /// <summary>
    /// 剥除注释（// 与 /* */）、字符串字面量（'..' 与 ".."）与反引号标识符（`..`）.
    /// </summary>
    private static string StripLiterals(string cypher)
    {
        var builder = new System.Text.StringBuilder(cypher.Length);
        var i = 0;
        while (i < cypher.Length)
        {
            var current = cypher[i];
            // 行注释
            if (current == '/' && i + 1 < cypher.Length && cypher[i + 1] == '/')
            {
                while (i < cypher.Length && cypher[i] != '\n')
                {
                    i++;
                }

                continue;
            }

            // 块注释
            if (current == '/' && i + 1 < cypher.Length && cypher[i + 1] == '*')
            {
                i += 2;
                while (i + 1 < cypher.Length && !(cypher[i] == '*' && cypher[i + 1] == '/'))
                {
                    i++;
                }

                i = Math.Min(i + 2, cypher.Length);
                continue;
            }

            // 字符串/标识符字面量：整体跳过（保留占位空格维持词边界）
            if (current is '\'' or '"' or '`')
            {
                var quote = current;
                builder.Append(' ');
                i++;
                while (i < cypher.Length)
                {
                    if (cypher[i] == '\\' && quote != '`' && i + 1 < cypher.Length)
                    {
                        i += 2;
                        continue;
                    }

                    if (cypher[i] == quote)
                    {
                        i++;
                        break;
                    }

                    i++;
                }

                builder.Append(' ');
                continue;
            }

            builder.Append(current);
            i++;
        }

        return builder.ToString();
    }

    private static string? FirstKeyword(string text)
    {
        var trimmed = text.TrimStart();
        if (trimmed.Length == 0)
        {
            return null;
        }

        var index = 0;
        while (index < trimmed.Length && !char.IsWhiteSpace(trimmed[index]) && trimmed[index] != '(')
        {
            index++;
        }

        return trimmed[..index].ToUpperInvariant();
    }

    private static bool ContainsWholeWord(string text, string keyword)
    {
        var upper = text.ToUpperInvariant();
        var index = upper.IndexOf(keyword, StringComparison.Ordinal);
        while (index >= 0)
        {
            var before = index == 0 || !char.IsLetterOrDigit(upper[index - 1]) && upper[index - 1] != '_';
            var after = index + keyword.Length >= upper.Length
                || !char.IsLetterOrDigit(upper[index + keyword.Length]) && upper[index + keyword.Length] != '_';
            if (before && after)
            {
                return true;
            }

            index = upper.IndexOf(keyword, index + keyword.Length, StringComparison.Ordinal);
        }

        return false;
    }
}
