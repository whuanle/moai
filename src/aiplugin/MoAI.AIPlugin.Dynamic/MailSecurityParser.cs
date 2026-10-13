using System;
using MailKit.Security;
using MoAI.Infra.Exceptions;

namespace MoAI.AIPlugin.Dynamic;

/// <summary>
/// 邮件安全模式解析工具：把配置里的字符串解析为 MailKit 的 <see cref="SecureSocketOptions"/>.
/// </summary>
public static class MailSecurityParser
{
    /// <summary>
    /// 解析安全模式；空值回落 ssl，未知值抛业务异常.
    /// </summary>
    /// <param name="security">安全模式：ssl（默认）/ starttls / none.</param>
    /// <param name="fieldName">配置字段名（用于错误提示）.</param>
    /// <returns>MailKit 安全选项.</returns>
    public static SecureSocketOptions Parse(string? security, string fieldName = "Security")
    {
        var value = (security ?? string.Empty).Trim().ToLowerInvariant();
        return value switch
        {
            "" or "ssl" or "tls" => SecureSocketOptions.SslOnConnect,
            "starttls" => SecureSocketOptions.StartTls,
            "none" => SecureSocketOptions.None,
            _ => throw new BusinessException(400, $"{fieldName} 只能是 ssl / starttls / none，当前值：{value}"),
        };
    }
}
