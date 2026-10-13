using MailKit.Security;
using MoAI.AIPlugin.Dynamic;
using MoAI.Infra.Exceptions;
using Xunit;

namespace MoAI.AIPlugin.Dynamic.Tests;

/// <summary>
/// 邮件安全模式解析测试.
/// </summary>
public class MailSecurityParserTests
{
    [Theory]
    [InlineData("", SecureSocketOptions.SslOnConnect)]
    [InlineData("ssl", SecureSocketOptions.SslOnConnect)]
    [InlineData("SSL", SecureSocketOptions.SslOnConnect)]
    [InlineData("tls", SecureSocketOptions.SslOnConnect)]
    [InlineData(" starttls ", SecureSocketOptions.StartTls)]
    [InlineData("none", SecureSocketOptions.None)]
    public void Parse_KnownValues_MapToMailKitOptions(string security, SecureSocketOptions expected)
    {
        Assert.Equal(expected, MailSecurityParser.Parse(security));
    }

    [Fact]
    public void Parse_UnknownValue_Throws()
    {
        var ex = Assert.Throws<BusinessException>(() => MailSecurityParser.Parse("tlsx"));

        Assert.Equal(400, ex.StatusCode);
        Assert.Contains("ssl / starttls / none", ex.Message, System.StringComparison.Ordinal);
    }
}
