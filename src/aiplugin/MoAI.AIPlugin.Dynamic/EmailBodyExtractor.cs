using System;
using System.Net;
using System.Text.RegularExpressions;
using MimeKit;

namespace MoAI.AIPlugin.Dynamic;

/// <summary>
/// 邮件正文提取工具：优先取 text/plain，缺省时从 text/html 剥标签得到可读文本，并按字符数截断.
/// </summary>
public static partial class EmailBodyExtractor
{
    /// <summary>
    /// style/script 整块剔除.
    /// </summary>
    [GeneratedRegex(@"<(style|script)\b[^>]*>[\s\S]*?</\1>", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex StyleScriptRegex();

    /// <summary>
    /// HTML 注释剔除.
    /// </summary>
    [GeneratedRegex(@"<!--[\s\S]*?-->", RegexOptions.CultureInvariant)]
    private static partial Regex CommentRegex();

    /// <summary>
    /// 标签剔除.
    /// </summary>
    [GeneratedRegex(@"<[^>]+>", RegexOptions.CultureInvariant)]
    private static partial Regex TagRegex();

    /// <summary>
    /// 连续空白折叠为单个空格.
    /// </summary>
    [GeneratedRegex(@"\s+", RegexOptions.CultureInvariant)]
    private static partial Regex WhitespaceRegex();

    /// <summary>
    /// 提取邮件的可读正文：text/plain 优先；只有 HTML 时剥去标签并解码实体；都没有返回空串.
    /// </summary>
    /// <param name="message">MIME 消息.</param>
    /// <returns>可读正文文本.</returns>
    public static string GetTextBody(MimeMessage message)
    {
        var text = message.TextBody;
        if (!string.IsNullOrWhiteSpace(text))
        {
            return text;
        }

        var html = message.HtmlBody;
        if (!string.IsNullOrWhiteSpace(html))
        {
            return StripHtml(html);
        }

        return string.Empty;
    }

    /// <summary>
    /// 把 HTML 粗略转换为可读文本：剔除 style/script/注释与标签、解码实体、折叠空白.
    /// </summary>
    /// <param name="html">HTML 原文.</param>
    /// <returns>可读文本.</returns>
    public static string StripHtml(string html)
    {
        if (string.IsNullOrEmpty(html))
        {
            return string.Empty;
        }

        var text = StyleScriptRegex().Replace(html, " ");
        text = CommentRegex().Replace(text, " ");
        text = TagRegex().Replace(text, " ");
        text = WebUtility.HtmlDecode(text);
        return WhitespaceRegex().Replace(text, " ").Trim();
    }

    /// <summary>
    /// 按最大字符数截断文本；maxChars ≤ 0 表示不截断.
    /// </summary>
    /// <param name="text">原文.</param>
    /// <param name="maxChars">最大字符数.</param>
    /// <returns>截断后的文本（超长时标注原文长度）.</returns>
    public static string Truncate(string text, int maxChars)
    {
        if (maxChars <= 0 || string.IsNullOrEmpty(text) || text.Length <= maxChars)
        {
            return text ?? string.Empty;
        }

        return text[..maxChars] + $"…（正文超长，已截断，原文共 {text.Length} 字符）";
    }
}
