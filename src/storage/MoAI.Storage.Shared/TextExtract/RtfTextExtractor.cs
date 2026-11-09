using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace MoAI.Storage.TextExtract;

/// <summary>
/// RTF（Rich Text Format）文本提取：控制词剥离器，输出纯文本.
/// 处理分组 {}、控制词（\par/\line/\tab/\uN/\'hh）、跳过字体表/颜色表/样式表/图片等 destination 组.
/// </summary>
public static class RtfTextExtractor
{
    /// <summary>
    /// 需要整体跳过的 destination 组控制词（组内容非正文：字体表/样式/图片/页眉页脚等）.
    /// </summary>
    private static readonly HashSet<string> SkipDestinations = new(StringComparer.Ordinal)
    {
        "fonttbl", "colortbl", "stylesheet", "info", "pict", "object", "header", "headerl", "headerr",
        "headerf", "footer", "footerl", "footerr", "footerf", "listtable", "listoverridetable",
        "latentstyles", "themedata", "colorschememapping", "datastore", "rsidtbl", "generator",
        "xmlnstbl", "fldinst", "shpinst", "shptxt", "nonshppict", "pn", "xe", "tc", "bkmkstart", "bkmkend",
    };

    /// <summary>
    /// 从流中提取 RTF 文本.
    /// </summary>
    /// <param name="stream">文件流（不要求可 Seek，内部整体缓冲）.</param>
    /// <param name="fileName">文件名，用于错误提示.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>剥离控制词后的纯文本.</returns>
    public static async Task<string> ExtractAsync(Stream stream, string fileName, CancellationToken cancellationToken)
    {
        if (stream.CanSeek)
        {
            stream.Seek(0, SeekOrigin.Begin);
        }

        using var buffered = new MemoryStream();
        await stream.CopyToAsync(buffered, cancellationToken);

        // RTF 是 ASCII 文本 + \'hh 字节转义，按 Latin1 解码保留原始字节，\uN 承载 Unicode
        var rtf = Encoding.Latin1.GetString(buffered.ToArray());
        var sb = new StringBuilder(rtf.Length);
        var groupSkipStack = new Stack<bool>();
        var skip = false;
        var ucSkip = 1;
        var index = 0;

        while (index < rtf.Length)
        {
            var ch = rtf[index];
            switch (ch)
            {
                case '{':
                    groupSkipStack.Push(skip);
                    index++;
                    break;
                case '}':
                    if (groupSkipStack.Count > 0)
                    {
                        skip = groupSkipStack.Pop();
                    }

                    index++;
                    break;
                case '\\':
                    index = ReadControl(rtf, index, sb, ref skip, ref ucSkip);
                    break;
                case '\n':
                case '\r':
                    // 控制词后的换行是语法分隔符，不是正文换行
                    index++;
                    break;
                default:
                    if (!skip)
                    {
                        sb.Append(ch);
                    }

                    index++;
                    break;
            }

            cancellationToken.ThrowIfCancellationRequested();
        }

        var text = sb.ToString().Replace("\r\n", "\n");
        return System.Text.RegularExpressions.Regex.Replace(text, @"\n{3,}", "\n\n").Trim();
    }

    /// <summary>
    /// 处理位于 <paramref name="index"/> 的控制序列，返回新的读取位置.
    /// </summary>
    private static int ReadControl(string rtf, int index, StringBuilder sb, ref bool skip, ref int ucSkip)
    {
        // 越过最后的孤立反斜杠
        if (index + 1 >= rtf.Length)
        {
            return index + 1;
        }

        var next = rtf[index + 1];

        // 转义符号：\\ \{ \} 输出字面字符
        if (next is '\\' or '{' or '}')
        {
            if (!skip)
            {
                sb.Append(next);
            }

            return index + 2;
        }

        // 不换行空格
        if (next == '~')
        {
            if (!skip)
            {
                sb.Append('\u00A0');
            }

            return index + 2;
        }

        // \* destination：由紧随的控制词决定是否跳过整组
        if (next == '*')
        {
            var destIndex = index + 2;
            var word = PeekControlWord(rtf, destIndex, out var wordEnd);
            if (word != null && SkipDestinations.Contains(word))
            {
                skip = true;
            }

            return destIndex;
        }

        // 控制词：字母序列 + 可选数字参数 + 可选单个空格
        if (!char.IsAsciiLetter(next))
        {
            // \'hh 十六进制字节转义（在组外遇到时不常见，按字面字节输出）
            if (next == '\'' && index + 3 < rtf.Length)
            {
                if (!skip)
                {
                    sb.Append((char)Convert.ToByte(rtf.Substring(index + 2, 2), 16));
                }

                return index + 4;
            }

            // 符号控制（如 \' 之外的未知），跳过反斜杠与符号
            return index + 2;
        }

        var end = index + 1;
        while (end < rtf.Length && char.IsAsciiLetter(rtf[end]))
        {
            end++;
        }

        var controlWord = rtf.Substring(index + 1, end - index - 1);

        // 可选数字参数（带可选负号）
        var hasParam = false;
        var param = 0;
        var paramStart = end;
        if (end < rtf.Length && (rtf[end] == '-' || char.IsAsciiDigit(rtf[end])))
        {
            hasParam = true;
            if (rtf[end] == '-')
            {
                end++;
            }

            var numberStart = end;
            while (end < rtf.Length && char.IsAsciiDigit(rtf[end]))
            {
                end++;
            }

            _ = int.TryParse(rtf[paramStart..end], out param);
        }

        // 控制词后可选的单个空格分隔符
        if (end < rtf.Length && rtf[end] == ' ')
        {
            end++;
        }

        switch (controlWord)
        {
            case "par":
            case "line":
                if (!skip)
                {
                    sb.Append('\n');
                }

                break;
            case "tab":
                if (!skip)
                {
                    sb.Append('\t');
                }

                break;
            case "u" when hasParam:
                if (!skip)
                {
                    // RTF Unicode 转义为 16 位有符号值
                    sb.Append((char)(short)param);
                }

                // \uN 后跟随 \ucN 个字节的降级表示，需跳过
                for (var i = 0; i < ucSkip; i++)
                {
                    end = SkipFallbackChar(rtf, end);
                }

                break;
            case "uc" when hasParam:
                ucSkip = param;
                break;
            case "bin" when hasParam && param > 0:
                end = Math.Min(end + param, rtf.Length);
                break;
            default:
                if (SkipDestinations.Contains(controlWord))
                {
                    skip = true;
                }

                break;
        }

        return end;
    }

    /// <summary>
    /// 预读控制词（不消费字符），用于 \* destination 判定.
    /// </summary>
    private static string? PeekControlWord(string rtf, int index, out int end)
    {
        end = index;
        if (index >= rtf.Length || !char.IsAsciiLetter(rtf[index]))
        {
            return null;
        }

        while (end < rtf.Length && char.IsAsciiLetter(rtf[end]))
        {
            end++;
        }

        return rtf[index..end];
    }

    /// <summary>
    /// 跳过 \uN 后的一个降级表示字符（\'hh 或转义符号或单字符）.
    /// </summary>
    private static int SkipFallbackChar(string rtf, int index)
    {
        if (index >= rtf.Length)
        {
            return index;
        }

        if (rtf[index] == '\\' && index + 1 < rtf.Length)
        {
            var next = rtf[index + 1];
            if (next == '\'' && index + 3 < rtf.Length)
            {
                return index + 4;
            }

            return index + 2;
        }

        return index + 1;
    }
}
