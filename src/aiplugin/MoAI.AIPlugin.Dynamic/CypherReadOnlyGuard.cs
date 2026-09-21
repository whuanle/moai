using System;
using System.Collections.Generic;

namespace MoAI.AIPlugin.Dynamic;

/// <summary>
/// Cypher 只读守卫：剥除注释与字符串字面量后，校验单条语句、只读首关键字白名单与全文写关键字黑名单.
/// </summary>
/// <remarks>
/// 黑名单含 CALL：v1 不开放图算法/存储过程（设计文档 §7.1）。
/// 首关键字白名单（MATCH/OPTIONAL/UNWIND/WITH/RETURN）与黑名单互为纵深防御，与 SqlReadOnlyGuard 同构。
/// </remarks>
internal static class CypherReadOnlyGuard
{
    /// <summary>查询文本最大长度.</summary>
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
            return $"Cypher 过长（超过 {MaxQueryLength} 字符），请缩小查询范围";
        }

        var text = StripCommentsAndLiterals(cypher);

        if (ContainsMultipleStatements(text))
        {
            return "只允许执行单条 Cypher 语句：不支持一次提交多条语句";
        }

        var leading = ReadLeadingKeyword(text);
        if (leading == null)
        {
            return "无法识别的 Cypher 语句";
        }

        if (!AllowedLeadingKeywords.Contains(leading))
        {
            return $"只允许执行只读 Cypher：不支持以 {leading} 开头的语句（仅允许 MATCH、OPTIONAL、UNWIND、WITH、RETURN）";
        }

        foreach (var keyword in ReadKeywords(text))
        {
            if (ForbiddenKeywords.Contains(keyword))
            {
                return $"只允许执行只读 Cypher：语句中不允许出现 {keyword}（本插件为只读查询，写操作请走图谱管理界面）";
            }
        }

        return null;
    }

    /// <summary>
    /// 剥除行注释、块注释与单双引号字符串字面量，替换为单个空格.
    /// </summary>
    private static string StripCommentsAndLiterals(string cypher)
    {
        var chars = new char[cypher.Length];
        var length = 0;
        var i = 0;
        while (i < cypher.Length)
        {
            var current = cypher[i];
            if (current == '/' && i + 1 < cypher.Length && cypher[i + 1] == '/')
            {
                chars[length++] = ' ';
                i = SkipToEndOfLine(cypher, i);
                continue;
            }

            if (current == '/' && i + 1 < cypher.Length && cypher[i + 1] == '*')
            {
                chars[length++] = ' ';
                i = SkipBlockComment(cypher, i);
                continue;
            }

            if (current == '\'' || current == '"')
            {
                chars[length++] = ' ';
                i = SkipQuoted(cypher, i, current);
                continue;
            }

            chars[length++] = current;
            i++;
        }

        return new string(chars, 0, length);
    }

    private static bool ContainsMultipleStatements(string text)
    {
        var seenSemicolon = false;
        foreach (var current in text)
        {
            if (current == ';')
            {
                seenSemicolon = true;
                continue;
            }

            if (seenSemicolon && !char.IsWhiteSpace(current))
            {
                return true;
            }
        }

        return false;
    }

    private static string? ReadLeadingKeyword(string text)
    {
        var index = 0;
        while (index < text.Length && char.IsWhiteSpace(text[index]))
        {
            index++;
        }

        var start = index;
        while (index < text.Length && IsKeywordChar(text[index]))
        {
            index++;
        }

        return index == start ? null : text[start..index].ToUpperInvariant();
    }

    private static IEnumerable<string> ReadKeywords(string text)
    {
        var index = 0;
        while (index < text.Length)
        {
            if (IsKeywordChar(text[index]))
            {
                var start = index;
                while (index < text.Length && IsKeywordChar(text[index]))
                {
                    index++;
                }

                yield return text[start..index].ToUpperInvariant();
            }
            else
            {
                index++;
            }
        }
    }

    /// <summary>
    /// 关键字字符：字母、数字、下划线。不含 $（参数前缀，$kgId 不参与关键字判定）.
    /// </summary>
    private static bool IsKeywordChar(char value) => char.IsLetterOrDigit(value) || value == '_';

    private static int SkipToEndOfLine(string cypher, int index)
    {
        while (index < cypher.Length && cypher[index] != '\n')
        {
            index++;
        }

        return index;
    }

    private static int SkipBlockComment(string cypher, int index)
    {
        index += 2;
        while (index + 1 < cypher.Length && !(cypher[index] == '*' && cypher[index + 1] == '/'))
        {
            index++;
        }

        return Math.Min(index + 2, cypher.Length);
    }

    private static int SkipQuoted(string cypher, int index, char quote)
    {
        index++;
        while (index < cypher.Length)
        {
            if (cypher[index] == '\\' && index + 1 < cypher.Length)
            {
                index += 2;
                continue;
            }

            if (cypher[index] == quote)
            {
                return index + 1;
            }

            index++;
        }

        return index;
    }
}
