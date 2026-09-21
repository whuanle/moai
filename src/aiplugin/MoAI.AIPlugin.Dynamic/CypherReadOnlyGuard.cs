using System;
using System.Collections.Generic;

namespace MoAI.AIPlugin.Dynamic;

/// <summary>
/// Cypher 只读守卫：剥除注释、字符串字面量与反引号标识符后，校验单条语句、只读首关键字白名单与全文写关键字黑名单.
/// </summary>
/// <remarks>
/// 黑名单含 CALL：v1 不开放图算法/存储过程（设计文档 §7.1）。
/// 首关键字白名单（MATCH/OPTIONAL/UNWIND/WITH/RETURN）与黑名单互为纵深防御，与 SqlReadOnlyGuard 同构。
/// 8000 长度上限按剥除前原始文本计（含字符串字面量），作为 O(n) 剥除的 DoS 边界。
/// </remarks>
internal static class CypherReadOnlyGuard
{
    /// <summary>查询文本最大长度.</summary>
    private const int MaxQueryLength = 8000;

    /// <summary>Cypher 为空的提示信息.</summary>
    private const string EmptyCypherMessage = "Cypher 不能为空";

    /// <summary>无法识别语句前缀时的提示信息.</summary>
    private const string UnrecognizedCypherMessage = "无法识别的 Cypher 语句";

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
            return EmptyCypherMessage;
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
            return UnrecognizedCypherMessage;
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
    /// 剥除行注释、块注释、单双引号字符串字面量与反引号标识符，替换为单个空格.
    /// </summary>
    /// <param name="cypher">原始 Cypher 文本.</param>
    /// <returns>剥离后的文本（被剥离的片段替换为空格，不改变其余字符的相对位置）.</returns>
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

            // 单双引号字符串与反引号标识符整体跳过：字面量内容（含疑似注释符、分号、写关键字）不参与判定
            if (current is '\'' or '"' or '`')
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

    /// <summary>
    /// 判断剥离后的文本里是否还存在第二条语句.
    /// </summary>
    /// <param name="text">剥离后的文本.</param>
    /// <returns>存在多余语句时为 <see langword="true"/>（末尾的单个分号不算）.</returns>
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

    /// <summary>
    /// 读取语句的首个关键字.
    /// </summary>
    /// <param name="text">剥离后的文本.</param>
    /// <returns>大写关键字；没有可识别关键字时返回 <see langword="null"/>.</returns>
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

    /// <summary>
    /// 按词边界切出文本中的所有关键字（统一转大写）.
    /// </summary>
    /// <param name="text">剥离后的文本.</param>
    /// <returns>关键字序列.</returns>
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
    /// 关键字字符：字母、数字、下划线与 $（参数前缀）。含 $ 使参数名/属性名整词参与切词，$kgId 切为 $KGID，不会撞黑名单.
    /// </summary>
    /// <param name="value">待判断字符.</param>
    /// <returns>字母、数字、下划线或美元符号时为 <see langword="true"/>.</returns>
    private static bool IsKeywordChar(char value) => char.IsLetterOrDigit(value) || value == '_' || value == '$';

    /// <summary>
    /// 跳到行尾.
    /// </summary>
    /// <param name="cypher">Cypher 文本.</param>
    /// <param name="index">起始下标.</param>
    /// <returns>行尾下标.</returns>
    private static int SkipToEndOfLine(string cypher, int index)
    {
        while (index < cypher.Length && cypher[index] != '\n')
        {
            index++;
        }

        return index;
    }

    /// <summary>
    /// 跳过块注释（未闭合时剥到文本结尾）.
    /// </summary>
    /// <param name="cypher">Cypher 文本.</param>
    /// <param name="index">块注释起始下标.</param>
    /// <returns>注释结束后的下标.</returns>
    private static int SkipBlockComment(string cypher, int index)
    {
        index += 2;
        while (index + 1 < cypher.Length && !(cypher[index] == '*' && cypher[index + 1] == '/'))
        {
            index++;
        }

        return Math.Min(index + 2, cypher.Length);
    }

    /// <summary>
    /// 跳过引号包裹的字面量/标识符。单双引号字符串用反斜杠转义；反引号标识符中反斜杠是字面字符、仅双写反引号转义，使守卫吞并区间与服务端词法精确重合.
    /// </summary>
    /// <param name="cypher">Cypher 文本.</param>
    /// <param name="index">引号之后的下标.</param>
    /// <param name="quote">引号字符.</param>
    /// <returns>闭合引号之后的下标（未闭合时返回文本长度）.</returns>
    private static int SkipQuoted(string cypher, int index, char quote)
    {
        index++;
        while (index < cypher.Length)
        {
            if (quote != '`' && cypher[index] == '\\' && index + 1 < cypher.Length)
            {
                index += 2;
                continue;
            }

            if (cypher[index] == quote)
            {
                if (quote == '`' && index + 1 < cypher.Length && cypher[index + 1] == quote)
                {
                    index += 2;
                    continue;
                }

                return index + 1;
            }

            index++;
        }

        return index;
    }
}
