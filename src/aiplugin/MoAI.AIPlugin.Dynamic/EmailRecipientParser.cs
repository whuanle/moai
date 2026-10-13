using System;
using System.Collections.Generic;
using System.Linq;
using MimeKit;
using MoAI.Infra.Exceptions;

namespace MoAI.AIPlugin.Dynamic;

/// <summary>
/// 邮件收件人解析工具：把「逗号/分号分隔的地址串」拆分、去重并逐个校验为合法邮箱.
/// </summary>
public static class EmailRecipientParser
{
    /// <summary>
    /// 地址之间的合法分隔符：逗号、分号、换行.
    /// </summary>
    private static readonly char[] _separators = [',', ';', '\r', '\n'];

    /// <summary>
    /// 把地址串拆分为去空格、按忽略大小写去重后的地址列表（不做格式校验）.
    /// </summary>
    /// <param name="raw">用户输入的地址串，可为 null.</param>
    /// <returns>归一化后的地址列表（保持首次出现顺序）.</returns>
    public static IReadOnlyList<string> Split(string? raw)
    {
        var result = new List<string>();
        if (string.IsNullOrWhiteSpace(raw))
        {
            return result;
        }

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var token in raw.Split(_separators, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (token.Length == 0)
            {
                continue;
            }

            if (seen.Add(token))
            {
                result.Add(token);
            }
        }

        return result;
    }

    /// <summary>
    /// 拆分并逐个校验地址；任一地址非法即抛业务异常.
    /// </summary>
    /// <param name="raw">地址串.</param>
    /// <param name="fieldName">请求参数名（用于错误提示，如 To/Cc/Bcc）.</param>
    /// <returns>解析出的邮箱地址列表.</returns>
    public static IReadOnlyList<string> Parse(string? raw, string fieldName)
    {
        var tokens = Split(raw);
        if (tokens.Count == 0)
        {
            throw new BusinessException(400, $"{fieldName} 不能为空，请填写至少一个邮箱地址");
        }

        foreach (var token in tokens)
        {
            if (!TryParseAddress(token, out _))
            {
                throw new BusinessException(400, $"{fieldName} 包含非法邮箱地址：{token}");
            }
        }

        return tokens;
    }

    /// <summary>
    /// 尝试解析单个邮箱地址（兼容「Name &lt;addr&gt;」形态，只取地址部分）.
    /// </summary>
    /// <remarks>
    /// MimeKit 的 <see cref="MailboxAddress.Parse"/> 对裸词、缺域地址过宽（如 <c>not-an-email</c> 也能解析），
    /// 解析后再做一次结构校验：恰好一个 @、本地部分与域名非空、域名至少含一个点且不含空白.
    /// </remarks>
    /// <param name="raw">单个地址或带显示名的地址.</param>
    /// <param name="address">解析出的纯地址.</param>
    /// <returns>是否合法.</returns>
    public static bool TryParseAddress(string raw, out string address)
    {
        address = string.Empty;
        try
        {
            var mailbox = MailboxAddress.Parse(raw);
            var value = mailbox.Address ?? string.Empty;
            if (!IsPlausibleEmailAddress(value))
            {
                return false;
            }

            address = value;
            return true;
        }
        catch (FormatException)
        {
            return false;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    /// <summary>
    /// 邮箱地址结构校验（务实版：单 @、非空两侧、域名至少一个点且无空白）.
    /// </summary>
    /// <param name="address">纯地址.</param>
    /// <returns>是否通过.</returns>
    private static bool IsPlausibleEmailAddress(string address)
    {
        if (string.IsNullOrWhiteSpace(address))
        {
            return false;
        }

        var parts = address.Split('@');
        if (parts.Length != 2 || parts[0].Length == 0 || parts[1].Length == 0)
        {
            return false;
        }

        return parts[1].Contains('.') && parts[1].IndexOfAny([' ', '\t']) < 0;
    }

    /// <summary>
    /// 解析单个可选地址字段（如 ReplyTo）：空值返回空串，非法值抛业务异常.
    /// </summary>
    /// <param name="raw">地址或带显示名的地址.</param>
    /// <param name="fieldName">请求参数名.</param>
    /// <returns>归一化后的纯地址；输入为空时返回空串.</returns>
    public static string ParseSingle(string? raw, string fieldName)
    {
        var value = (raw ?? string.Empty).Trim();
        if (value.Length == 0)
        {
            return string.Empty;
        }

        if (!TryParseAddress(value, out var address))
        {
            throw new BusinessException(400, $"{fieldName} 不是合法的邮箱地址：{value}");
        }

        return address;
    }
}
