using System.Globalization;
using System.Text;
using SmartFormat;
using SmartFormat.Core.Settings;

namespace MoAI.Auth.Services;

/// <summary>
/// 验证码邮件模板插值：把模板中的 <c>{code}</c> <c>{siteName}</c> <c>{expireMinutes}</c> 占位符替换为实际值，基于 SmartFormat 实现.
/// <para>白名单外的一切花括号（未匹配变量、JSON 片段等）按字面量保留，策略与 variable 模块 TeamVariableTemplate 一致
/// （auth 为平台底座层，不引用 variable 业务层，故独立实现）.</para>
/// </summary>
public static class EmailCodeTemplate
{
    private const char OpenSentinel = '\uE000';
    private const char CloseSentinel = '\uE001';

    private static readonly SmartFormatter Formatter = CreateFormatter();

    /// <summary>
    /// 将模板中的 <c>{key}</c> 替换为 <paramref name="values"/> 中对应的值.
    /// </summary>
    /// <param name="template">模板文本，空串原样返回.</param>
    /// <param name="values">变量表（code / siteName / expireMinutes）.</param>
    /// <returns>插值后的文本；未匹配到变量的占位符保留原文.</returns>
    public static string Render(string template, IReadOnlyDictionary<string, string> values)
    {
        if (template.Length == 0)
        {
            return template;
        }

        var prepared = new StringBuilder(template.Length);
        var index = 0;
        while (index < template.Length)
        {
            if (template[index] == '{')
            {
                var closeIndex = MatchPlaceholder(template, index);
                if (closeIndex > 0 && values.ContainsKey(template.Substring(index + 1, closeIndex - index - 1)))
                {
                    prepared.Append(template, index, closeIndex - index + 1);
                    index = closeIndex + 1;
                    continue;
                }

                prepared.Append(OpenSentinel);
                index++;
                continue;
            }

            if (template[index] == '}')
            {
                prepared.Append(CloseSentinel);
                index++;
                continue;
            }

            prepared.Append(template[index]);
            index++;
        }

        var result = Formatter.Format(CultureInfo.InvariantCulture, prepared.ToString(), values);
        return result.Replace(OpenSentinel, '{').Replace(CloseSentinel, '}');
    }

    /// <summary>
    /// 匹配 <paramref name="openIndex"/> 处 <c>{</c> 后跟变量 key 与 <c>}</c> 的占位符.
    /// </summary>
    /// <returns>闭合 <c>}</c> 的下标；不是合法占位符时返回 -1.</returns>
    private static int MatchPlaceholder(string template, int openIndex)
    {
        var i = openIndex + 1;
        if (i >= template.Length || !char.IsAsciiLetter(template[i]))
        {
            return -1;
        }

        while (i < template.Length && (char.IsAsciiLetterOrDigit(template[i]) || template[i] == '_'))
        {
            i++;
        }

        return i < template.Length && template[i] == '}' ? i : -1;
    }

    private static SmartFormatter CreateFormatter()
    {
        var formatter = Smart.CreateDefaultSmartFormat();
        // 模板已被白名单清洗，此处兜底：解析/格式化错误保持原样输出，不抛异常
        formatter.Settings.Parser.ErrorAction = ParseErrorAction.MaintainTokens;
        formatter.Settings.Formatter.ErrorAction = FormatErrorAction.MaintainTokens;
        return formatter;
    }
}
