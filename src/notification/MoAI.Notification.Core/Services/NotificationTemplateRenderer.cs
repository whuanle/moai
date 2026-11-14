using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;

namespace MoAI.Notification.Services;

/// <summary>
/// 通知模板插值核心：把模板文本中的 <c>{{key}}</c> 双花括号占位符替换为变量值.
/// <para>占位符形态与主流云邮件平台（内容可原样复制到平台提审）对齐：变量形如 <c>{{name}}</c>，
/// 变量名仅字母/数字/下划线且字母开头。白名单策略：只有变量表中存在的 key 会被求值替换，
/// 其余一切花括号（JSON 片段、<c>{{other}}</c>、<c>{0}</c> 等）一律按字面量保留，不会被误替换或抛错。</para>
/// </summary>
public static class NotificationTemplateRenderer
{
    private static readonly Regex PlaceholderRegex = new("\\{\\{([A-Za-z][A-Za-z0-9_]*)\\}\\}", RegexOptions.Compiled);

    /// <summary>
    /// 将模板中的 <c>{{key}}</c> 替换为 <paramref name="values"/> 中对应的值.
    /// </summary>
    /// <param name="template">模板文本.</param>
    /// <param name="values">变量值表，可为空（全部占位符按字面量保留）.</param>
    /// <returns>插值后的文本.</returns>
    public static string Format(string template, IReadOnlyDictionary<string, string>? values)
    {
        if (template.Length == 0 || values is not { Count: > 0 })
        {
            return template;
        }

        return PlaceholderRegex.Replace(template, match =>
        {
            var name = match.Groups[1].Value;
            return values.TryGetValue(name, out var value) ? value : match.Value;
        });
    }

    /// <summary>
    /// 按出现顺序（去重）提取模板正文中的变量名（腾讯云短信 TemplateParamSet 为位置数组，顺序必须与平台模板一致）.
    /// </summary>
    /// <param name="template">模板文本.</param>
    /// <returns>返回变量名列表.</returns>
    public static List<string> ExtractNames(string template)
    {
        var names = new List<string>();
        foreach (Match match in PlaceholderRegex.Matches(template))
        {
            var name = match.Groups[1].Value;
            if (!names.Contains(name))
            {
                names.Add(name);
            }
        }

        return names;
    }

    /// <summary>
    /// HTML 降级为纯文本（非邮件渠道发送 HTML 模板时使用）：去 style/script 与标签、解码实体、收敛空行.
    /// </summary>
    /// <param name="html">HTML 文本.</param>
    /// <returns>返回纯文本.</returns>
    public static string HtmlToPlain(string html)
    {
        if (html.Length == 0)
        {
            return html;
        }

        var text = Regex.Replace(html, "<(style|script|head)[^>]*>.*?</\\1>", string.Empty, RegexOptions.Singleline | RegexOptions.IgnoreCase);
        text = Regex.Replace(text, "<br\\s*/?>", "\n", RegexOptions.IgnoreCase);
        text = Regex.Replace(text, "</(p|div|tr|li|h[1-6]|table|blockquote)>", "\n", RegexOptions.IgnoreCase);
        text = Regex.Replace(text, "<[^>]+>", string.Empty);
        text = WebUtility.HtmlDecode(text);
        text = text.Replace("\u00a0", " ");

        var lines = text.Split('\n').Select(line => line.TrimEnd()).ToList();
        var result = new StringBuilder();
        var blank = true;
        foreach (var line in lines)
        {
            if (line.Length == 0)
            {
                if (blank)
                {
                    continue;
                }

                blank = true;
                result.Append('\n');
                continue;
            }

            blank = false;
            result.Append(line).Append('\n');
        }

        return result.ToString().TrimEnd();
    }
}
